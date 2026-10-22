using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private IEnumerator InstallNpcInvitationFlowHouse()
        {
            var state = ContentCatalog.Create(6123);
            state.dealRulesStartWeek = 1;
            // A saved, lawful house where the real generator has reasons to ask the player.
            // No NPC placement, reservation, arrival, clock suspension or witness proof is forged.
            foreach (var edge in state.relationships) edge.score = 0;
            foreach (var npc in state.Active.Where(person => !person.isPlayer))
            {
                state.relationships.Single(edge => edge.fromId == npc.id && edge.toId == state.playerId).score = 10;
                state.relationships.Single(edge => edge.fromId == state.playerId && edge.toId == npc.id).score = 60;
            }
            state.relationships.Single(edge => edge.fromId == ContentCatalog.MayaId && edge.toId == state.playerId).score = 90;
            // Jamie's authored Strategic trait qualifies the information-sharing rung below
            // partnership warmth; Taylor's Competitive/Confrontational traits do not.
            state.relationships.Single(edge => edge.fromId == "jamie-roberts" && edge.toId == state.playerId).score = 35;
            NpcDeals.Propose(state);
            Assert.That(NpcDeals.Pending(state).Count, Is.GreaterThanOrEqualTo(2), "The actual generator produces distinct offers.");
            Assert.That(NpcDeals.Pending(state).Any(row => row.proposerId == ContentCatalog.MayaId && row.type == DealKind.Partnership), Is.True);
            Assert.That(NpcDeals.Pending(state).Any(row => row.proposerId == "jamie-roberts" && row.type == DealKind.InformationSharing), Is.True);
            state.socialActions = EpisodeEngine.SocialActionBudget(state);
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
            Assert.That(NpcRead<bool>("npcDiagnosticsSuspended"), Is.False);
            Assert.That(NpcRead<bool>("npcApproachDiagnosticsSuppressed"), Is.False,
                "This flow keeps ordinary NPC approach planning enabled.");
            Assert.That(director.ShowOverview(), Is.True);
            yield return WaitForVisibleNpcInvitation();
        }

        private IEnumerator WaitForVisibleNpcInvitation()
        {
            float deadline = Time.realtimeSinceStartup + 35f;
            while (!director.NpcInvitationIsVisible && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.NpcAutonomyReady, Is.True, director.NpcAutonomyDiagnostic);
            Assert.That(director.NpcInvitationIsVisible, Is.True,
                "A motivated NPC should reach the player and display the actual offer invitation.");
            Assert.That(director.CurrentNpcInvitationRecordId, Is.Not.Null);
            Assert.That(director.IsOverview, Is.True, "The world invitation remains available on the normal overhead map.");
            Assert.That(director.IsBriefing, Is.False);
            var npc = SceneComponents<HouseNpc>().Single(person => person.Id == director.CurrentNpcInvitationNpcId);
            Assert.That(Vector3.Distance(npc.transform.position, player.transform.position), Is.LessThanOrEqualTo(2.8f));
        }

        [UnityTest]
        public IEnumerator NpcInvitationFlow_RealApproachAndInteractOpenOnlyTheExactOfferAtZeroActions()
        {
            yield return InstallNpcInvitationFlowHouse();
            if (Application.isBatchMode) yield return CaptureFraming("dialogue-npc-invitation", settle: false);
            string offerId = director.CurrentNpcInvitationRecordId, fromId = director.CurrentNpcInvitationNpcId;
            var before = director.Snapshot;
            Assert.That(EpisodeEngine.SocialActionsSpent(before), Is.EqualTo(EpisodeEngine.SocialActionBudget(before)));
            Assert.That(NpcDeals.Pending(before).Any(row => row.id == offerId && row.proposerId == fromId), Is.True);
            director.Interact();
            Assert.That(director.NpcInvitationIsVisible, Is.False);
            Assert.That(director.TalkingToId, Is.EqualTo(fromId));
            Assert.That(director.CurrentConversationCategory, Is.EqualTo(EpisodeDirector.DealsCategoryCaption));
            Assert.That(director.InvitedDealRecordId, Is.EqualTo(offerId));
            Assert.That(director.Snapshot.acceptedCommandIds, Is.EqualTo(before.acceptedCommandIds));
            Assert.That(EpisodeEngine.SocialActionsSpent(director.Snapshot), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before)));
            Assert.That(NpcDeals.Pending(director.Snapshot).Any(row => row.id == offerId), Is.True,
                "Hearing a question does not answer it.");
            yield return null; yield return null;
            var accept = ButtonWithCaption(EpisodeHud.DealAcceptCaption);
            Assert.That(accept.IsInteractable(), Is.True, "Existing incoming offer replies are free at zero actions.");
            Assert.That(TagOn(accept), Does.Contain(EpisodeDirector.FreeTag));
            var justBeforeAnswer = director.Snapshot;
            accept.onClick.Invoke();
            Assert.That(director.Snapshot.acceptedCommandIds.Count, Is.EqualTo(justBeforeAnswer.acceptedCommandIds.Count + 1));
            Assert.That(EpisodeEngine.SocialActionsSpent(director.Snapshot), Is.EqualTo(EpisodeEngine.SocialActionsSpent(justBeforeAnswer)));
            Assert.That(NpcDeals.Pending(director.Snapshot).Any(row => row.id == offerId), Is.False);
            Assert.That(director.IsConversationExchangePlaying, Is.True);
        }

        [UnityTest]
        public IEnumerator NpcInvitationFlow_NotNowIsFreeAndOldCallbacksCannotHearTheReloadedOffer()
        {
            yield return InstallNpcInvitationFlowHouse();
            var bubble = director.GetComponent<HouseNpcInvitationBubble>();
            Assert.That(bubble, Is.Not.Null);
            string deferredId = director.CurrentNpcInvitationRecordId;
            var staleHear = bubble.HearControl.onClick;
            var before = director.Snapshot;
            byte[] bytes = File.ReadAllBytes(director.SavePath);
            bubble.NotNowControl.onClick.Invoke();
            Assert.That(director.NpcInvitationIsVisible, Is.False);
            Assert.That(director.IsPanelOpen, Is.False);
            Assert.That(NpcDeals.Pending(director.Snapshot).Any(row => row.id == deferredId), Is.True);
            AssertEquivalent(before, director.Snapshot);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(director.SavePath));
            staleHear.Invoke();
            Assert.That(director.TalkingToId, Is.Null);

            // Deferral is transient. Loading the saved question may invite again, but the
            // retained previous view must never gain authority over that new invitation.
            director.LoadNow();
            Assert.That(director.ShowOverview(), Is.True);
            yield return WaitForVisibleNpcInvitation();
            var reloaded = director.Snapshot;
            staleHear.Invoke();
            Assert.That(director.NpcInvitationIsVisible, Is.True);
            Assert.That(director.TalkingToId, Is.Null);
            AssertEquivalent(reloaded, director.Snapshot);
            director.Interact();
            Assert.That(director.TalkingToId, Is.Not.Null);
            Assert.That(director.InvitedDealRecordId, Is.Not.Null);
        }
    }
}
