using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gamesim.House
{
    /// <summary>A transient furniture reservation. No needs, social commands or saved activity clock.</summary>
    public sealed class HouseActivityLease
    {
        public string ActorId { get; internal set; }
        public HouseInteractionAnchor Anchor { get; internal set; }
        public bool Released { get; internal set; }
        internal string Token;
        internal Vector3 Position,Approach;
        internal float Facing;
    }

    public sealed partial class HouseMeetingCoordinator
    {
        private sealed class ActivityEntry
        {
            public HouseActivityLease lease;
            public Actor npc;
            public HousePlayerController player;
            public HouseFurniturePose presentation;
            public bool presentationRegistered;
        }
        private readonly List<ActivityEntry> activities=new List<ActivityEntry>();

        public bool ActivityAnchorAvailable(HouseInteractionAnchor anchor)
            => !disposed && !HasCompetitionStage && anchor!=null && anchor.isActiveAndEnabled && anchor.gameObject.scene==rooms.Scene
                && !VenueInUse(anchor.VenueId) && !activities.Any(entry=>entry.lease.Anchor==anchor);

        public bool TryReserveActivity(string actorId,HouseInteractionAnchor anchor,out HouseActivityLease lease,out string reason)
        {
            lease=null;reason=null;
            if(paused || !IsReady || HasCompetitionStage || !ActivityAnchorAvailable(anchor)
                || !actors.TryGetValue(actorId,out var actor) || !eligible.Contains(actorId)
                || actor.motion==null || actor.motion.LeaseId!=null)
            {reason="This place or houseguest is busy.";return false;}
            var candidate=NewActivityLease(actorId,anchor);
            if(!actor.motion.TryReserveAndPath(candidate.Token,anchor.Approach))
            {reason="This activity has no complete clear route.";return false;}
            activities.Add(new ActivityEntry{lease=candidate,npc=actor});lease=candidate;return true;
        }

        public bool TryReservePlayerActivity(HousePlayerController player,HouseInteractionAnchor anchor,out HouseActivityLease lease,out string reason)
        {
            lease=null;reason=null;
            if(player==null || player.gameObject.scene!=rooms.Scene || HasCompetitionStage || !ActivityAnchorAvailable(anchor))
            {reason="This place is busy or unavailable.";return false;}
            if(player.Agent==null || !rooms.TrySampleFloor(anchor.Approach,player.Agent.radius,filter,.25f,out var feet,out var room)
                || room!=anchor.RoomId || !rooms.HasCapsuleClearance(feet,player.Agent.radius,player.Agent.height,player.transform))
            {reason="The activity approach is obstructed.";return false;}
            var candidate=NewActivityLease("player",anchor);
            if(!player.TryBeginActivityMove(candidate,anchor.Approach,out reason))return false;
            activities.Add(new ActivityEntry{lease=candidate,player=player});lease=candidate;return true;
        }

        private static HouseActivityLease NewActivityLease(string id,HouseInteractionAnchor anchor)
            => new HouseActivityLease{ActorId=id,Anchor=anchor,Token="activity:"+System.Guid.NewGuid().ToString("N"),
                Position=anchor.Position,Approach=anchor.Approach,Facing=anchor.Facing};

        public bool TryGetActivity(string actorId,out HouseActivityLease lease)
        {
            var entry=activities.FirstOrDefault(row=>row.lease.ActorId==actorId);
            lease=entry?.lease;return entry!=null;
        }

        public bool ActivityValid(HouseActivityLease lease)
        {
            var entry=activities.FirstOrDefault(row=>ReferenceEquals(row.lease,lease));
            if(entry==null || lease.Released || lease.Anchor==null || !lease.Anchor.isActiveAndEnabled
                || lease.Anchor.gameObject.scene!=rooms.Scene || (lease.Anchor.Position-lease.Position).sqrMagnitude>.0025f
                || (lease.Anchor.Approach-lease.Approach).sqrMagnitude>.0025f
                || Mathf.Abs(Mathf.DeltaAngle(lease.Facing,lease.Anchor.Facing))>1f)return false;
            if(entry.presentationRegistered && (entry.presentation==null || !entry.presentation.isActiveAndEnabled || !entry.presentation.Active))return false;
            return entry.npc!=null ? eligible.Contains(entry.npc.id) && entry.npc.npc!=null && entry.npc.motion!=null
                    && entry.npc.motion.IsBound && entry.npc.motion.LeaseId==lease.Token
                : entry.player!=null && entry.player.IsActivityMoveValid(lease);
        }

        public bool RegisterActivityPresentation(HouseActivityLease lease,HouseFurniturePose presentation)
        {
            var entry=activities.FirstOrDefault(row=>ReferenceEquals(row.lease,lease));
            var root=ActivityRoot(entry);var actor=root!=null ? root.gameObject : null;
            if(entry==null || actor==null || presentation==null || presentation.gameObject!=actor || !presentation.isActiveAndEnabled || !presentation.Active)return false;
            entry.presentation=presentation;entry.presentationRegistered=true;return true;
        }

        public bool ActivityPaused(HouseActivityLease lease)
            => paused && activities.Any(entry=>ReferenceEquals(entry.lease,lease) && entry.npc!=null);

        public bool ActivityArrived(HouseActivityLease lease)
        {
            if(!ActivityValid(lease))return false;
            var entry=activities.First(row=>ReferenceEquals(row.lease,lease));
            return entry.npc!=null ? entry.npc.motion.HasArrivedAt(lease.Token) : entry.player.ActivityHasArrived(lease);
        }

        public void ReleaseActivity(HouseActivityLease lease)
        {
            var entry=activities.FirstOrDefault(row=>ReferenceEquals(row.lease,lease));
            if(entry==null)return;
            activities.Remove(entry);lease.Released=true;
            var actorRoot=ActivityRoot(entry);
            if(actorRoot!=null)actorRoot.GetComponent<HouseFurniturePose>()?.End();
            if(entry.npc?.motion!=null)entry.npc.motion.Release(lease.Token);
            if(entry.player!=null)entry.player.ReleaseActivityMove(lease);
        }

        private static Transform ActivityRoot(ActivityEntry entry)
        {
            if(entry==null)return null;
            if(entry.npc!=null)return entry.npc.motion!=null ? entry.npc.motion.transform : null;
            return entry.player!=null ? entry.player.transform : null;
        }

        public void ReleaseActivities()
        {foreach(var entry in activities.ToArray())ReleaseActivity(entry.lease);}

        private void RetireInvalidActivities()
        {foreach(var entry in activities.ToArray())if(!ActivityValid(entry.lease))ReleaseActivity(entry.lease);}

        private bool ActivityOwnsMotion(HouseNpcMotion motion)
            => activities.Any(entry=>entry.npc!=null && entry.npc.motion==motion && entry.lease.Token==motion.LeaseId);

        private void YieldActivity(string actorId)
        {if(TryGetActivity(actorId,out var lease))ReleaseActivity(lease);}
    }
}
