using Gamesim.House;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeDirector
    {
        private HouseInteractionAnchor episodeDestination, competitionDestination;
        private HouseInteractionAnchor Destination(bool competition)
        {
            var anchor=competition ? competitionDestination : episodeDestination;
            if(anchor==null)
            {
                HouseInteractionAnchors.EnsureDefaults(gameObject.scene);
                HouseInteractionAnchors.TryFind(gameObject.scene,competition ? HouseInteractionAnchors.CompetitionDestination
                    : HouseInteractionAnchors.EpisodeDestination,0,out anchor);
                if(competition)competitionDestination=anchor;else episodeDestination=anchor;
            }
            return anchor != null && anchor.isActiveAndEnabled ? anchor : null;
        }

        private bool AtCompetitionPhase => EpisodeEngine.IsCompetition(projected?.phase ?? EpisodePhase.Social);

        private Vector3 ResolveStationPosition()
        {
            var anchor = Destination(AtCompetitionPhase);
            return anchor != null ? anchor.Approach : Vector3.positiveInfinity;
        }

        internal void GoToPlayDestination(bool competition)
        {
            if (!IsReady || blockedRecovery) return;
            // The player's own decision waiting - the nominations, the veto meeting, the ballot, the
            // final choice - is the episode screen's whole business, so the Episode button opens it
            // where the player stands (UI-UX plan Q2). Free time, and every beat with nothing of
            // theirs to decide, still walks them to the nomination room.
            if (!competition && OpenWaitingDecision()) return;
            var anchor = Destination(competition);
            if (anchor == null) return;
            if (competition == AtCompetitionPhase) GoToStation();
            else if (!playerIsActive) GoToRoom(anchor.RoomId);
            else GoToRoomFloor(anchor.RoomId, null, anchor.Approach);
        }

        /// <summary>
        /// The episode screen opened where the player stands, when a decision of their own waits on it
        /// (<see cref="Presentation.WaitingDecision"/>): the Episode button's way in from anywhere in the house. As
        /// the walk's own press does, it replaces whatever the player was doing - an errand, the diary
        /// room, a seat - and the walk it replaces does not resume when the screen closes. The screen
        /// opens as every panel does, with the house's due time flushed first (PauseNpcSocialForPanel).
        /// Already open, it is left as it is, picks and all; over the notebook opened from it, the
        /// press goes back to it as it was left (ReturnToDecision). False, with nothing changed, when
        /// nothing of the player's waits. Public for tests.
        /// </summary>
        public bool OpenWaitingDecision()
        {
            if (!IsReady || blockedRecovery || player == null) return false;
            if (!Presentation.WaitingDecision.Waits(projected, challengeActive)) return false;
            // Under a ceremony card or a board the press does what it always did.
            if (Presentation.CeremonyOverlays.OnScreen || Presentation.CompetitionGameScreen.AnyDrawn) return false;
            if (phaseOpen) return true;
            if (journalOpen && decisionPeek != null) { ReturnToDecision(); return phaseOpen; }
            ClosePanels();
            CancelTravel();
            EndDiaryVisit(true); CloseHouseActivities(true);
            player.StopHere();
            PauseNpcSocialForPanel();
            if (blockedRecovery) return false;
            ClosePanels(); phaseOpen = true; player.SetInputEnabled(false); cameraRig.ControlsEnabled = false; Render(); return true;
        }

        /// <summary>The room the phase's screen stands in: the nomination room, or the yard for a competition.</summary>
        private string StationRoomId()
        {
            return Destination(AtCompetitionPhase)?.RoomId;
        }
    }
}
