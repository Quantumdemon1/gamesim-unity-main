using Gamesim.House;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeDirector
    {
        private HouseInteractionAnchor episodeDestination, competitionDestination;
        private Vector3 ResolveStationPosition()
        {
            bool competition=EpisodeEngine.IsCompetition(projected?.phase ?? EpisodePhase.Social);
            var anchor=competition ? competitionDestination : episodeDestination;
            if(anchor==null)
            {
                HouseInteractionAnchors.EnsureDefaults(gameObject.scene);
                HouseInteractionAnchors.TryFind(gameObject.scene,competition ? HouseInteractionAnchors.CompetitionDestination
                    : HouseInteractionAnchors.EpisodeDestination,0,out anchor);
                if(competition)competitionDestination=anchor;else episodeDestination=anchor;
            }
            return anchor!=null && anchor.isActiveAndEnabled ? anchor.Approach : Vector3.positiveInfinity;
        }

        /// <summary>The room the phase's screen stands in: the nomination room, or the yard for a competition.</summary>
        private string StationRoomId()
        {
            ResolveStationPosition();
            var anchor=EpisodeEngine.IsCompetition(projected?.phase ?? EpisodePhase.Social) ? competitionDestination : episodeDestination;
            return anchor!=null && anchor.isActiveAndEnabled ? anchor.RoomId : null;
        }
    }
}
