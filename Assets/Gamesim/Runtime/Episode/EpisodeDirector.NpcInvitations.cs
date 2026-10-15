using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeDirector
    {
        private NpcInvitation npcInvitationNow;
        private HouseNpcInvitationLease npcInvitationVisit;
        private HouseMeetingCoordinator npcInvitationCoordinator;
        private HouseNpcInvitationBubble npcInvitationBubble;
        private object npcInvitationView;
        private long npcInvitationLoad;
        private float npcInvitationNextScan;
        private float npcInvitationNextPresentation;
        private EpisodeState npcInvitationSource;
        private EpisodeState npcInvitationCheckedSource;
        private bool npcInvitationStillCurrent;
        private IReadOnlyList<NpcInvitation> npcInvitationCandidates = Array.Empty<NpcInvitation>();
        private readonly HashSet<string> npcInvitationDeferred = new HashSet<string>(StringComparer.Ordinal);
        private string npcInvitationSession;
        private int npcInvitationWeek;
        private EpisodePhase npcInvitationPhase;

        public string CurrentNpcInvitationRecordId => npcInvitationNow?.RecordId;
        public string CurrentNpcInvitationNpcId => npcInvitationNow?.NpcId;
        public bool NpcInvitationIsVisible => npcInvitationBubble != null && npcInvitationBubble.IsVisible;
        public string InvitedDealRecordId => focusedNpc != null ? invitedDealId : null;

        private bool NpcInvitationHouseAvailable => IsReady && isActiveAndEnabled && projected != null
            && playerIsActive && !IsHouseUnderChrome && !FrontDoorUp && !TourIsUp
            && !durableCommitInProgress && !npcDiagnosticsSuspended && !npcSaveSuspended && !npcWorldFailed
            && !OpeningOwnsHouse && !IsCeremonyStaged && walkingOutId == null && !competitionArenaStaging
            && !challengeActive && headingToNpcId == null && !headingToStation && arrivingIn == null
            && talkSpot == null && !IsPlayerHouseActivityActive && NpcInvitation.PhaseAllows(projected.phase)
            && npcMeetings != null && npcMeetings.IsReady && !npcMeetings.HasSceneStage
            && !npcMeetings.HasOpeningStage && !npcMeetings.HasCeremonyStage && !npcMeetings.HasCompetitionStage;

        private void TickNpcInvitations()
        {
            if (projected != null && (npcInvitationSession != projected.sessionId
                || npcInvitationWeek != projected.week || npcInvitationPhase != projected.phase))
            {
                CancelNpcInvitation(); npcInvitationDeferred.Clear(); npcInvitationSource = null;
                npcInvitationSession = projected.sessionId; npcInvitationWeek = projected.week; npcInvitationPhase = projected.phase;
            }
            if (!NpcInvitationHouseAvailable) { CancelNpcInvitation(); return; }
            if (npcInvitationNow != null)
            {
                if (npcInvitationLoad != loadGeneration || !NpcInvitationCurrentInInstalledState()
                    || !ReferenceEquals(npcInvitationCoordinator, npcMeetings)
                    || npcInvitationVisit == null || npcInvitationVisit.Released)
                {
                    // An interrupted/expired visit waits rather than repeatedly seeking the player
                    // in the same window. Its saved offer remains available in Deals and the notebook.
                    npcInvitationDeferred.Add(npcInvitationNow.RecordId);
                    CancelNpcInvitation();
                }
                else
                {
                    // House/view cancellation above is immediate. Route/clearance/visibility
                    // checks and bubble refresh run with the existing 10 Hz world presentation.
                    // Input callbacks always perform their own complete current proof.
                    if (Time.unscaledTime < npcInvitationNextPresentation) return;
                    npcInvitationNextPresentation = Time.unscaledTime + .1f;
                    if (!npcMeetings.NpcInvitationValid(npcInvitationVisit))
                    {
                        npcInvitationDeferred.Add(npcInvitationNow.RecordId);
                        CancelNpcInvitation();
                        return;
                    }
                    if (npcMeetings.NpcInvitationCanSpeak(npcInvitationVisit, out var anchor))
                    {
                        if (npcInvitationBubble == null) npcInvitationBubble = gameObject.AddComponent<HouseNpcInvitationBubble>();
                        object view = npcInvitationView; long generation = npcInvitationLoad;
                        var invitation = npcInvitationNow;
                        npcInvitationBubble.Show(projected.Find(invitation.NpcId).name, invitation.OpeningLine, TextScale, anchor,
                            () => HearNpcInvitation(view, generation, invitation),
                            () => DeferNpcInvitation(view, generation, invitation), view);
                    }
                    else npcInvitationBubble?.Hide();
                    return;
                }
            }
            if (Time.unscaledTime < npcInvitationNextScan) return;
            npcInvitationNextScan = Time.unscaledTime + 1f;
            if (!ReferenceEquals(npcInvitationSource, projected))
            { npcInvitationSource = projected; npcInvitationCandidates = NpcInvitation.Available(projected); }
            foreach (var invitation in npcInvitationCandidates)
            {
                if (npcInvitationDeferred.Contains(invitation.RecordId) || !invitation.IsCurrent(projected)
                    || !NpcInvitationConversationAvailable(projected, invitation.NpcId)) continue;
                if (!npcMeetings.TryReserveNpcInvitation(invitation.NpcId, player, out var visit, out _)) continue;
                npcInvitationNow = invitation; npcInvitationVisit = visit; npcInvitationCoordinator = npcMeetings;
                npcInvitationLoad = loadGeneration; npcInvitationView = new object();
                npcInvitationNextPresentation = 0f;
                return;
            }
        }

        private bool NpcInvitationCurrentInInstalledState()
        {
            if (!ReferenceEquals(npcInvitationCheckedSource, projected))
            {
                npcInvitationCheckedSource = projected;
                npcInvitationStillCurrent = npcInvitationNow != null && npcInvitationNow.IsCurrent(projected)
                    && NpcInvitationConversationAvailable(projected, npcInvitationNow.NpcId);
            }
            return npcInvitationStillCurrent;
        }

        // Match the conversation's existing public phase/window gate. A busy houseguest must
        // not summon the player to an offer the conversation cannot display in this phase.
        private static bool NpcInvitationConversationAvailable(EpisodeState state, string npcId) =>
            state.phase == EpisodePhase.Social || state.phase == EpisodePhase.Campaign
            || StrategyRules.IsDecider(state, npcId)
            || (EpisodeEngine.WeekRulesOn(state) && EpisodeEngine.Window(state) != Windows.None);

        private bool NpcInvitationCallbackCurrent(object view, long generation, NpcInvitation invitation) =>
            view != null && ReferenceEquals(view, npcInvitationView) && ReferenceEquals(invitation, npcInvitationNow)
            && generation == loadGeneration && NpcInvitationHouseAvailable && invitation.IsCurrent(projected)
            && NpcInvitationConversationAvailable(projected, invitation.NpcId)
            && npcInvitationCoordinator != null && npcInvitationCoordinator.NpcInvitationCanSpeak(npcInvitationVisit, out _);

        private void HearNpcInvitation(object view, long generation, NpcInvitation invitation)
        {
            if (!NpcInvitationCallbackCurrent(view, generation, invitation)) return;
            string npcId = invitation.NpcId, recordId = invitation.RecordId;
            npcInvitationDeferred.Add(recordId);
            CancelNpcInvitation();
            // Hearing the question opens its existing offer. It does not answer it, move the
            // player, spend an action or add a new saved record.
            if (!TryOpenNpc(npcId)) return;
            if (!NpcDeals.Pending(projected).Any(row => row.proposerId == npcId && row.id == recordId)) return;
            PrepareConversationNavigation(projected, projected.Find(npcId));
            conversationCategory = DealsCategoryCaption; invitedDealId = recordId;
            Render();
        }

        private void DeferNpcInvitation(object view, long generation, NpcInvitation invitation)
        {
            if (!NpcInvitationCallbackCurrent(view, generation, invitation)) return;
            npcInvitationDeferred.Add(invitation.RecordId);
            CancelNpcInvitation();
        }

        private bool TryHearVisibleNpcInvitation()
        {
            if (!NpcInvitationIsVisible || !NpcInvitationCallbackCurrent(npcInvitationView, npcInvitationLoad, npcInvitationNow)) return false;
            HearNpcInvitation(npcInvitationView, npcInvitationLoad, npcInvitationNow);
            return true;
        }

        private bool TryDeferVisibleNpcInvitation()
        {
            if (!NpcInvitationIsVisible || !NpcInvitationCallbackCurrent(npcInvitationView, npcInvitationLoad, npcInvitationNow)) return false;
            DeferNpcInvitation(npcInvitationView, npcInvitationLoad, npcInvitationNow);
            return true;
        }

        private HouseNpc VisibleNpcInvitationSpeaker()
        {
            if (!NpcInvitationIsVisible || npcInvitationNow == null || npcInvitationLoad != loadGeneration
                || !NpcInvitationHouseAvailable || !NpcInvitationCurrentInInstalledState()) return null;
            return housemates.FirstOrDefault(npc => npc != null && npc.Id == npcInvitationNow.NpcId && npc.gameObject.activeInHierarchy);
        }

        private void CancelNpcInvitation()
        {
            npcInvitationView = null;
            npcInvitationCheckedSource = null;
            npcInvitationBubble?.Hide();
            npcInvitationCoordinator?.ReleaseNpcInvitation(npcInvitationVisit);
            npcInvitationNow = null; npcInvitationVisit = null; npcInvitationCoordinator = null;
        }

        private void ResetNpcInvitationsForLoad()
        {
            CancelNpcInvitation(); npcInvitationDeferred.Clear(); npcInvitationSource = null;
            npcInvitationCandidates = Array.Empty<NpcInvitation>(); npcInvitationSession = null;
            npcInvitationNextScan = 0f;
        }
    }
}
