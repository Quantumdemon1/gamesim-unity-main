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
            var anchor = Destination(competition);
            if (anchor == null) return;
            if (competition == AtCompetitionPhase) GoToStation();
            else if (!playerIsActive) GoToRoom(anchor.RoomId);
            else GoToRoomFloor(anchor.RoomId, null, anchor.Approach);
        }

        /// <summary>The room the phase's screen stands in: the nomination room, or the yard for a competition.</summary>
        private string StationRoomId()
        {
            return Destination(AtCompetitionPhase)?.RoomId;
        }
    }
}
