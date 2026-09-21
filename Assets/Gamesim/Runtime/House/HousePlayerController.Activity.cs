using UnityEngine;

namespace Gamesim.House
{
    public sealed partial class HousePlayerController
    {
        private object activityOwner;
        private Vector3 activityDestination, previousDestination;
        private bool activityPaused, previousHadPath;

        /// <summary>One transient movement owner, with a real complete route and no root teleport.</summary>
        public bool TryBeginActivityMove(object owner,Vector3 desired,out string reason)
        {
            reason=null;
            if(owner==null || activityOwner!=null || !isActiveAndEnabled || Agent==null || !Agent.enabled || !Agent.isOnNavMesh)
            {reason="The player already has a movement owner or cannot reach the house floor.";return false;}
            bool hadPath=Agent.hasPath;var destination=hadPath ? Agent.destination : transform.position;
            if(!TrySetReachablePath(desired,Mathf.Min(.2f,destinationSampleRadius)))
            {reason="There is no complete route to this activity.";return false;}
            previousHadPath=hadPath;previousDestination=destination;
            activityDestination=Agent.destination;activityOwner=owner;activityPaused=false;
            ApplyPauseState();return true;
        }

        public bool IsActivityMoveValid(object owner)
            => ReferenceEquals(owner,activityOwner) && owner!=null && isActiveAndEnabled && Agent!=null
                && Agent.enabled && Agent.isOnNavMesh && (Agent.hasPath || NearActivityDestination());

        public bool ActivityHasArrived(object owner)
            => IsActivityMoveValid(owner) && NearActivityDestination() && !Agent.pathPending
                && Agent.velocity.sqrMagnitude<.04f;

        public bool PauseActivityMove(object owner,bool paused)
        {
            if(!ReferenceEquals(owner,activityOwner) || owner==null)return false;
            activityPaused=paused;ApplyPauseState();
            if(paused && Agent!=null && Agent.enabled && Agent.isOnNavMesh)Agent.velocity=Vector3.zero;
            return true;
        }

        public bool ReleaseActivityMove(object owner)
        {
            if(owner==null || !ReferenceEquals(owner,activityOwner))return false;
            activityOwner=null;activityPaused=false;
            if(Agent!=null && Agent.enabled && Agent.isOnNavMesh)
            {
                Agent.ResetPath();Agent.velocity=Vector3.zero;
                if(previousHadPath)TrySetReachablePath(previousDestination,destinationSampleRadius);
            }
            previousHadPath=false;
            // SetInputEnabled calls made by a newly opened panel win over the old input state.
            ApplyPauseState();return true;
        }

        private bool NearActivityDestination()
            => Vector3.Distance(transform.position,activityDestination)<=Mathf.Max(.25f,Agent.stoppingDistance+.1f);

        private void ValidateActivityOwner()
        {
            if(activityOwner is Object unityOwner && unityOwner==null)ReleaseActivityMove(activityOwner);
        }

        private void OnDisable()
        {
            if(activityOwner!=null)ReleaseActivityMove(activityOwner);
        }
    }
}
