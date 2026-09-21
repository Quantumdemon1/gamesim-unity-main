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
    }
}
