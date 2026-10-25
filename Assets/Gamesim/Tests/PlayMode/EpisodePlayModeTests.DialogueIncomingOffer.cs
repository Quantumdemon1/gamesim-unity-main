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
        private string dialogueIncomingOfferId, dialogueIncomingProposer;

        [UnityTest]
        public IEnumerator DialogueCampaign_RealIncomingOfferAcceptsAtCaseAndResumesAfterReload()
        {
            yield return DialogueIncomingOfferRoute(finishNode: false, accept: true);
        }

        [UnityTest]
        public IEnumerator DialogueCampaign_RealIncomingOfferDeclinesAtFinishAndResumesAfterReload()
        {
            yield return DialogueIncomingOfferRoute(finishNode: true, accept: false);
        }

        private IEnumerator DialogueIncomingOfferRoute(bool finishNode, bool accept)
        {
            yield return InstallDialogueIncomingOfferCampaign();
            // Use the actual category -> vote ask -> approach UI, as the existing paid UI tests do.
            yield return OpenDialogueUiEntry();
            string episodeId = director.Snapshot.dialogue.episodes.Single().id;
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
            var stalePaidChoice = ButtonWithCaption(paidCaption).onClick;
            AssertDialogueUiDisk(paused);
            Assert.That(NpcInvitation.Available(paused).Any(i => i.RecordId == dialogueIncomingOfferId), Is.True,
                "The incoming question is the actual saved generator output, not a manufactured UI invitation.");

            yield return LeaveDialogueUiConversation();
            Assert.That(director.ShowOverview(), Is.True);
            var justLeft = director.Snapshot;
            byte[] justLeftBytes = File.ReadAllBytes(director.SavePath);
            stalePaidChoice.Invoke();
            AssertDialogueUiUnchanged(justLeft, justLeftBytes);
            AssertDialogueIncomingPause(paused, director.Snapshot, episodeId, node, steps);

            // Do NOT HoldTheHouseForTheFixture: that disposes the invitation coordinator.
            // Ordinary world movement/clock revisions are allowed during this physical approach.
            // The existing helper has a 35-real-second deadline and checks arrival/proximity.
            yield return WaitForVisibleNpcInvitation();
            // Hearing opens the conversation, and opening it first flushes the house's free time not
            // yet sampled: with a whole second due, that commits one NPC clock tick inside the press -
            // the house's revision, not the press's. The bubble's 10 Hz check and the world's 100 ms
            // sampler keep separate clocks, so the first visible frame can fall in that tail (late in
            // a long run about one press in twenty did). Press on a frame with no second due, read and
            // never written, so the strict check below holds the press alone to "commits nothing".
            // The world samples within 0.1 s; nothing from here to the press yields.
            float settle = Time.realtimeSinceStartup + 5f;
            while ((!director.NpcInvitationIsVisible || NpcWholeSecondDue()) && Time.realtimeSinceStartup < settle)
                yield return null;
            Assert.That(director.NpcInvitationIsVisible, Is.True,
                "The invitation should still be showing once the house's due second has committed.");
            Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(dialogueIncomingOfferId));
            Assert.That(director.CurrentNpcInvitationNpcId, Is.EqualTo(dialogueIncomingProposer));
            var waiting = director.Snapshot;
            AssertDialogueIncomingPause(paused, waiting, episodeId, node, steps);
            var invitation = NpcInvitation.Available(waiting).Single(i => i.RecordId == dialogueIncomingOfferId);
            var bubble = director.GetComponent<HouseNpcInvitationBubble>();
            Assert.That(bubble, Is.Not.Null);
            Assert.That(bubble.CurrentText, Is.EqualTo(invitation.OpeningLine));
            var hear = ButtonWithCaption(HouseNpcInvitationBubble.HearCaption);
            Assert.That(hear, Is.SameAs(bubble.HearControl), "Use the visible, raycast-reachable invitation control.");
            byte[] waitingBytes = File.ReadAllBytes(director.SavePath);
            Assert.That(NpcWholeSecondDue(), Is.False,
                "The house still owes a whole second of free time, so opening the conversation would commit its clock tick.");
            hear.onClick.Invoke();
            AssertDialogueUiUnchanged(waiting, waitingBytes);
            Assert.That(director.NpcInvitationIsVisible, Is.False);
            Assert.That(director.TalkingToId, Is.EqualTo(dialogueIncomingProposer));
            Assert.That(director.InvitedDealRecordId, Is.EqualTo(dialogueIncomingOfferId));
            Assert.That(director.CurrentConversationCategory, Is.EqualTo(EpisodeDirector.DealsCategoryCaption));
            yield return Frames(2);
            Assert.That(ShownText(), Does.Contain("THEIR OFFER"));
            Assert.That(ButtonWithCaptionOrNull(paidCaption), Is.Null,
                "The genuine invitation owns the offer view, rather than a hidden paid-choice invocation.");
            var offerView = director.Snapshot;
            byte[] offerViewBytes = File.ReadAllBytes(director.SavePath);
            stalePaidChoice.Invoke();
            AssertDialogueUiUnchanged(offerView, offerViewBytes);
            AssertDialogueIncomingPause(paused, offerView, episodeId, node, steps);

            var answer = ButtonWithCaption(accept ? EpisodeHud.DealAcceptCaption : EpisodeHud.DealDeclineCaption);
            Assert.That(answer.IsInteractable(), Is.True);
            Assert.That(TagOn(answer), Does.Contain(EpisodeDirector.FreeTag));
            var beforeAnswer = director.Snapshot;
            string beforeAnswerJson = JsonUtility.ToJson(beforeAnswer);
            answer.onClick.Invoke(); // This, not Submit/Engine.Apply, is the route under test.
            var answered = director.Snapshot;
            Assert.That(answered.revision, Is.EqualTo(beforeAnswer.revision + 1));
            Assert.That(answered.acceptedCommandIds.Count, Is.EqualTo(beforeAnswer.acceptedCommandIds.Count + 1));
            Assert.That(JsonUtility.ToJson(beforeAnswer), Is.EqualTo(beforeAnswerJson));
            AssertDialogueIncomingPause(beforeAnswer, answered, episodeId, node, steps);
            var answeredDeal = CommitmentReferences.Deals(answered).Single(d => d.id == dialogueIncomingOfferId);
            Assert.That(answeredDeal.status, Is.EqualTo(accept ? DealStatus.Active : DealStatus.Declined));
            Assert.That(NpcDeals.Pending(answered).Any(d => d.id == dialogueIncomingOfferId), Is.False);

            // A detached engine is an outcome oracle only. Reuse the actual UI-minted command ID
            // so exact state comparison includes both RNG streams and all accepted-ID/history rows.
            // RespondToDeal legitimately draws the main RNG: do not assert that it is unchanged.
            var expectedCommand = new EpisodeCommand
            {
                id = answered.acceptedCommandIds.Last(), actorId = beforeAnswer.playerId,
                expectedPhase = beforeAnswer.phase, expectedRevision = beforeAnswer.revision,
                kind = EpisodeCommandKind.RespondToDeal, targetId = dialogueIncomingOfferId,
                text = accept ? EpisodeEngine.AcceptDeal : "decline",
            };
            var expected = new EpisodeEngine(beforeAnswer).Apply(expectedCommand);
            Assert.That(expected.accepted, Is.True, expected.reason);
            Assert.That(expected.duplicate, Is.False);
            AssertEquivalent(expected.state, answered);
            AssertDialogueUiDisk(answered);
            byte[] answeredBytes = File.ReadAllBytes(director.SavePath);
            stalePaidChoice.Invoke();
            AssertDialogueUiUnchanged(answered, answeredBytes);

            // The invitation has actually completed. This existing reload helper verifies both
            // primary and backup bytes, then holds the new world only for deterministic resume.
            yield return ReloadDialogueUiBytes();
            yield return TalkTo(dialogueUiVoter);
            AssertDialogueUiNode(episodeId, node, steps);
            var resumed = director.Snapshot;
            byte[] resumedBytes = File.ReadAllBytes(director.SavePath);
            stalePaidChoice.Invoke();
            AssertDialogueUiUnchanged(resumed, resumedBytes);
            AssertDialogueIncomingPause(answered, resumed, episodeId, node, steps);
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
            Assert.That(DialogueUiRemaining(completed), Is.Zero);
            Assert.That(completed.windowActions, Is.EqualTo(paused.windowActions));
            Assert.That(completed.socialActions, Is.EqualTo(paused.socialActions));
            Assert.That(completed.outOfPhaseSocialActions, Is.EqualTo(paused.outOfPhaseSocialActions));
            Assert.That(DialogueRules.Active(completed), Is.Null);
            Assert.That(completed.storylines.Single(c => c.id == episodeId).endingId, Is.EqualTo("answered"));
            Assert.That(completed.storylines.Single(c => c.id == episodeId).path.Select(p => p.optionId),
                Is.EqualTo(new[] { "open-case", "appeal-personally", "make-plea" }));
            Assert.That(completed.houseEvents.Count(e => e.cycleId == episodeId), Is.EqualTo(3));
            Assert.That(completed.dialogue.grants, Is.Empty);
            var time = completed.dialogue.episodes.Single(e => e.id == episodeId).time;
            Assert.That(time.reason, Is.EqualTo("answered"));
            Assert.That(new[] { time.proposedDelta, time.appliedDelta, time.spentRevision,
                time.spentSequence, time.spentWindowAction }, Is.All.EqualTo(0));
            Assert.That(CommitmentReferences.Deals(completed).Single(d => d.id == dialogueIncomingOfferId).status,
                Is.EqualTo(accept ? DealStatus.Active : DealStatus.Declined));
            yield return ReloadDialogueUiBytes();
            AssertEquivalent(completed, director.Snapshot);
        }

        private IEnumerator InstallDialogueIncomingOfferCampaign()
        {
            // Seed 14 is a retained real-command diagnostic witness, not a save with altered
            // nominees, scores, RNG, AP, history, offer terms, invitation state or world leases.
            // A source change invalidating that witness fails this bounded test; no repair/skip.
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 14);
            ShippedRules.ApplyFresh(fresh);
            Assert.That(fresh.dialogueRulesVersion, Is.Zero);
            fresh.dialogueRulesVersion = 1;
            string original = JsonUtility.ToJson(fresh);
            var engine = new EpisodeEngine(fresh);
            EpisodeState fixture = null;
            for (int step = 0; step < 96; step++)
            {
                var s = engine.Snapshot;
                if (s.week != 1 || s.phase == EpisodePhase.Finished) break;
                if (s.phase != EpisodePhase.Campaign)
                {
                    DialogueUiApply(engine, NextCommand(s));
                    continue;
                }
                Assert.That(s.nominees, Does.Contain(s.playerId));
                CloseDialogueUiOldAsks(engine);
                for (int debit = 0; debit < 16 && DialogueUiRemaining(engine.Snapshot) > 1; debit++)
                {
                    s = engine.Snapshot;
                    var talk = DialogueUiCommand(s, EpisodeCommandKind.Talk);
                    talk.targetId = s.nominees.First(id => id != s.playerId);
                    DialogueUiApply(engine, talk);
                    CloseDialogueUiOldAsks(engine);
                }
                fixture = engine.Snapshot;
                break;
            }
            Assert.That(fixture, Is.Not.Null, "Seed 14 did not reach the real first-week Campaign within 96 commands.");
            Assert.That(JsonUtility.ToJson(fresh), Is.EqualTo(original));
            Assert.That(DialogueUiRemaining(fixture), Is.EqualTo(1));
            Assert.That((fixture.unifiedCommitmentRulesVersion, fixture.allWeekRulesStartWeek), Is.EqualTo((2, 1)));
            dialogueUiVoter = EpisodeEngine.Voters(fixture).Where(c => !c.isPlayer).Select(c => c.id)
                .FirstOrDefault(id => DialogueRules.CanStart(fixture, id, out _));
            Assert.That(dialogueUiVoter, Is.Not.Null);
            var offer = NpcDeals.Pending(fixture).Single(d => !KnownBallots.IsVoteDeal(d.type));
            Assert.That(offer.type, Is.EqualTo(DealKind.TargetAgreement));
            Assert.That(offer.proposerId, Is.EqualTo("jordan-taylor"));
            dialogueIncomingOfferId = offer.id; dialogueIncomingProposer = offer.proposerId;
            Assert.That(NpcInvitation.Available(fixture).Any(i => i.RecordId == offer.id), Is.True);
            Assert.That(EpisodeValidation.TryValidate(fixture, out string error), Is.True, error);
            TestContext.Out.WriteLine("Actual incoming-offer UI base: seed 14, revision " + fixture.revision
                + ", offer " + offer.id + ", speaker " + offer.proposerId + ", paid voter " + dialogueUiVoter + ".");
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            // Install synchronously before yielding: the unrelated outgoing world's next frame
            // must not save over the just-written fixture. LoadNow uses the real save reader and
            // normal cast/world installation without permanently suspending the incoming world.
            director.LoadNow();
            // Ordinary player chrome pauses clock work while inspecting the loaded bytes;
            // no field/lease/arrival/clock diagnostic manipulation is used for the invitation.
            director.OpenJournal();
            yield return Frames(2);
            yield return SkipReveals();
            AssertEquivalent(fixture, director.Snapshot);
            AssertDialogueUiDisk(fixture);
            Assert.That(NpcRead<bool>("npcDiagnosticsSuspended"), Is.False);
            Assert.That(NpcRead<bool>("npcApproachDiagnosticsSuppressed"), Is.False);
        }

        // Opening any panel flushes the house's unsampled free time (PauseNpcSocialForPanel ->
        // FlushNpcWholeTicks), and that commits an NPC clock tick only when the carried fraction plus
        // the unsampled remainder reaches a whole second. False means a panel opened now commits nothing.
        private bool NpcWholeSecondDue() =>
            NpcRead<double>("npcFrameFraction") + NpcRead<double>("npcWorldFraction") >= 1d;

        private static void AssertDialogueIncomingPause(EpisodeState before, EpisodeState after,
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
        }
    }
}
