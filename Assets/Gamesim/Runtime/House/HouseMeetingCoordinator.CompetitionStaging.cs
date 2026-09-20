using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.House
{
    public sealed partial class HouseMeetingCoordinator
    {
        private sealed class CompetitionLease
        {
            public Actor actor;
            public HouseInteractionAnchor anchor;
            public Vector3 position;
            public Vector3 approach;
            public float facing;
            public string token;
        }
        private readonly List<CompetitionLease> competitionLeases = new List<CompetitionLease>();
        public bool HasCompetitionStage => competitionLeases.Count > 0;
        public int CompetitionStageCount => competitionLeases.Count;
        public int CompetitionArrivals
        {
            get
            {
                int count=0;
                foreach(var lease in competitionLeases)if(lease.actor.motion.HasArrivedAt(lease.token))count++;
                return count;
            }
        }

        /// <summary>Presentation can borrow idle actors only. Meeting leases and saved conversations win.</summary>
        public bool BeginCompetitionStage(IReadOnlyList<string> ids,IReadOnlyList<HouseInteractionAnchor> anchors,out string reason)
        {
            reason=null;
            if(!IsReady||HasCompetitionStage||leases.Count!=0||ids==null||anchors==null||ids.Count!=anchors.Count||ids.Count==0)
            {reason="Houseguests are not available for arena staging.";return false;}
            var unique=new HashSet<string>();var uniqueAnchors=new HashSet<HouseInteractionAnchor>();
            for(int i=0;i<ids.Count;i++)
            {
                if(!unique.Add(ids[i])||!actors.TryGetValue(ids[i],out var actor)||!eligible.Contains(ids[i])
                    ||actor.motion==null||actor.motion.LeaseId!=null||anchors[i]==null||!uniqueAnchors.Add(anchors[i])
                    ||!anchors[i].isActiveAndEnabled||anchors[i].RoomId!="Yard"||anchors[i].gameObject.scene!=rooms.Scene)
                {reason="The stage request contains an unavailable houseguest or anchor.";return false;}
                for(int earlier=0;earlier<i;earlier++)
                    if((anchors[i].Approach-anchors[earlier].Approach).sqrMagnitude<1)
                    {reason="Arena places must leave clear space between participants.";return false;}
            }
            for(int i=0;i<ids.Count;i++)
            {
                var actor=actors[ids[i]];var anchor=anchors[i];
                string token="competition:"+i+":"+System.Guid.NewGuid().ToString("N");
                actor.motion.SetPaused(false);
                if(!actor.motion.TryReserveAndPath(token,anchor.Approach))
                {
                    actor.motion.SetPaused(paused);EndCompetitionStage();
                    reason="The arena route for "+actor.npc.DisplayName+" is unavailable. The eligible field remains listed.";
                    return false;
                }
                competitionLeases.Add(new CompetitionLease{actor=actor,anchor=anchor,position=anchor.Position,
                    approach=anchor.Approach,facing=anchor.Facing,token=token});
            }
            return true;
        }

        public bool ValidateCompetitionStage(out string reason)
        {
            foreach(var lease in competitionLeases)
                if(lease.anchor==null||!lease.anchor.isActiveAndEnabled||(lease.anchor.Position-lease.position).sqrMagnitude>.0025f
                    ||(lease.anchor.Approach-lease.approach).sqrMagnitude>.0025f||Mathf.Abs(Mathf.DeltaAngle(lease.anchor.Facing,lease.facing))>.5f
                    ||lease.actor.motion==null||!lease.actor.motion.IsBound||lease.actor.motion.LeaseId!=lease.token)
                {reason="Arena placement changed or a route became unavailable.";EndCompetitionStage();return false;}
            reason=null;return HasCompetitionStage;
        }

        public void SetCompetitionStagePaused(bool value)
        {foreach(var lease in competitionLeases)lease.actor.motion.SetPaused(value);}

        public bool CompetitionActorArrived(string id)
        {
            foreach(var lease in competitionLeases)
                if(lease.actor.id==id)return lease.actor.motion.HasArrivedAt(lease.token);
            return false;
        }

        public void EndCompetitionStage()
        {
            foreach(var lease in competitionLeases)
                if(lease.actor.motion!=null){lease.actor.motion.Release(lease.token);lease.actor.motion.SetPaused(paused);}
            competitionLeases.Clear();
        }

        private bool CompetitionOwnsMotion(HouseNpcMotion motion)
        {
            foreach(var lease in competitionLeases)
                if(lease.actor.motion==motion&&motion.LeaseId==lease.token)return true;
            return false;
        }
    }
}
