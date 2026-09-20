using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeDirector
    {
        private GameObject competitionArenaRoot;
        private Material competitionArenaMaterial;
        private Mesh competitionStationMesh;
        private readonly Dictionary<string,HouseInteractionAnchor> competitionArenaActors=new Dictionary<string,HouseInteractionAnchor>();
        private readonly List<HouseSeatPresentation> competitionAudienceSeats=new List<HouseSeatPresentation>();
        private readonly object competitionPlayerOwner=new object();
        private HouseInteractionAnchor competitionPlayerStation;
        private Vector3 competitionPlayerStationPosition;
        private float competitionPlayerStationFacing;
        private float competitionAssemblyDeadline;
        private string competitionArenaStatus, competitionAudienceStatus;
        private bool competitionArenaStaging;

        private bool BeginCompetitionArena(EpisodeState state)
        {
            EndCompetitionArena();
            npcMeetings?.ReleaseActivities();
            var floor=gameObject.scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<BoxCollider>())
                .FirstOrDefault(c=>c.name=="Competition yard floor");
            if(floor==null || player==null || player.Agent==null || !HouseRoomQuery.TryCreate(gameObject.scene,out var rooms,out _))
            { competitionArenaStatus="The competition floor is unavailable. Return when its route is ready."; return false; }
            var bounds=floor.bounds;
            competitionArenaRoot=new GameObject("Competition stage runtime");
            competitionArenaRoot.transform.SetParent(floor.transform,false);
            var scale=floor.transform.lossyScale;
            competitionArenaRoot.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
            var filter=new NavMeshQueryFilter{agentTypeID=player.Agent.agentTypeID,areaMask=player.Agent.areaMask};
            var candidates=new List<Vector3>();
            for(float z=bounds.min.z+1.5f;z<bounds.max.z-1.5f;z+=1.6f)
                for(float x=bounds.min.x+2f;x<bounds.max.x-2f;x+=1.8f)
                    candidates.Add(new Vector3(x,bounds.max.y,z));
            var preferred=new Vector3(bounds.center.x,bounds.max.y,bounds.min.z+2f);
            // The player uses a real complete path under an owner token. Ordinary input remains
            // locked; neither the navigation root nor its collider is teleported or reconfigured.
            foreach(var wanted in candidates.OrderBy(p=>(p-preferred).sqrMagnitude))
            {
                if(!rooms.TrySampleFloor(wanted,player.Agent.radius,filter,.25f,out var position,out var room)
                    || room!="Yard" || !rooms.HasCapsuleClearance(position,player.Agent.radius,player.Agent.height,player.transform)
                    || !player.TryBeginActivityMove(competitionPlayerOwner,position,out _))continue;
                competitionPlayerStation=HouseInteractionAnchor.Create(competitionArenaRoot.transform,"competition-player","Yard",0,position,180,false);
                competitionPlayerStationPosition=position; competitionPlayerStationFacing=competitionPlayerStation.Facing;
                break;
            }
            if(competitionPlayerStation==null)
            { EndCompetitionArena(); competitionArenaStatus="Your competition station has no clear route. Try again after the path is clear."; return false; }
            competitionArenaStaging=true; competitionAssemblyDeadline=Time.unscaledTime+30;
            var contestants=new HashSet<string>(EpisodeEngine.CompetitionPlayers(state).Select(c=>c.id));
            var ids=new List<string>(); var anchors=new List<HouseInteractionAnchor>();
            var used=new List<Vector3>{competitionPlayerStationPosition};
            competitionAudienceStatus="Other houseguests are unavailable for staging; the eligible field is listed.";
            bool castAvailable=npcMeetings!=null && npcMeetings.IsReady && state.npcSocial.pending.Count==0 && npcMeetings.LeaseCount==0;
            if(castAvailable)
            {
                // Audience seating can only borrow actual, authored furniture. If no suitable
                // seat is available the audience stands at a clear floor anchor instead.
                var seats=HouseInteractionAnchors.InScene(gameObject.scene)
                    .Where(a=>a.isActiveAndEnabled && a.Seated && a.RoomId=="Yard" && a.transform.parent!=null
                        && a.transform.parent.name=="bb_set_lounger").OrderBy(a=>a.VenueId).ThenBy(a=>a.Slot).ToArray();
                var usedSeats=new HashSet<HouseInteractionAnchor>();
                foreach(var actor in state.Active.Where(c=>!c.isPlayer).OrderBy(c=>contestants.Contains(c.id)?0:1).ThenBy(c=>c.id))
                {
                    var npc=housemates.FirstOrDefault(n=>n!=null && n.Id==actor.id);
                    var capsule=npc!=null ? npc.GetComponent<CapsuleCollider>() : null;
                    if(capsule==null || (npc.GetComponent<HouseSeatPresentation>()?.Active ?? false))continue;
                    HouseInteractionAnchor anchor=null;
                    var existingSeat=npc.GetComponent<HouseSeatPresentation>();
                    if(!contestants.Contains(actor.id) && (existingSeat==null || existingSeat.isActiveAndEnabled))
                        foreach(var seat in seats.Where(a=>!usedSeats.Contains(a)))
                            if(rooms.TrySampleFloor(seat.Approach,capsule.radius,filter,.25f,out var approach,out var room)
                                && room=="Yard" && (approach-seat.Approach).sqrMagnitude<.09f
                                && used.All(p=>(p-approach).sqrMagnitude>=1.6f)
                                && rooms.HasCapsuleClearance(approach,capsule.radius,capsule.height,npc.transform))
                            {anchor=seat;usedSeats.Add(seat);break;}
                    var actorPreferred=contestants.Contains(actor.id) ? preferred : new Vector3(bounds.min.x+2f,bounds.max.y,bounds.center.z);
                    if(anchor==null)
                        foreach(var wanted in candidates.OrderBy(p=>(p-actorPreferred).sqrMagnitude))
                        {
                            if(!rooms.TrySampleFloor(wanted,capsule.radius,filter,.25f,out var position,out var room)
                                || room!="Yard" || used.Any(p=>(p-position).sqrMagnitude<1.6f)
                                || !rooms.HasCapsuleClearance(position,capsule.radius,capsule.height,npc.transform))continue;
                            anchor=HouseInteractionAnchor.Create(competitionArenaRoot.transform,
                                contestants.Contains(actor.id)?"competition-contestant":"competition-audience","Yard",ids.Count,
                                position,contestants.Contains(actor.id)?180:Mathf.Atan2(bounds.center.x-position.x,bounds.center.z-position.z)*Mathf.Rad2Deg,false);
                            break;
                        }
                    if(anchor==null)continue;
                    ids.Add(actor.id); anchors.Add(anchor); used.Add(anchor.Approach); competitionArenaActors[actor.id]=anchor;
                }
                if(ids.Count>0 && npcMeetings.BeginCompetitionStage(ids,anchors,out var reason))
                    competitionAudienceStatus=ids.Count<state.Active.Count(c=>!c.isPlayer) ? "Some houseguests are unavailable; the eligible field remains listed." : "";
                else { EndCompetitionCast(); competitionAudienceStatus="Other houseguests could not reach their stage places; the eligible field is listed."; }
            }
            var shader=Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Unlit/Color");
            if(shader!=null)
            {
                competitionArenaMaterial=new Material(shader){name="Competition award accent"};
                competitionArenaMaterial.color=state.phase==EpisodePhase.Veto?UiTheme.Award:UiTheme.Gold;
                CreateCompetitionStationMesh();
                AddCompetitionStationMarker(competitionPlayerStation,"Your competition station");
                foreach(var pair in competitionArenaActors.Where(pair=>contestants.Contains(pair.Key)))
                    AddCompetitionStationMarker(pair.Value,state.phase==EpisodePhase.Veto?"Veto station":"HoH station");
            }
            var sign=new GameObject("Award and discipline",typeof(TextMeshPro));
            sign.transform.SetParent(competitionArenaRoot.transform,false);
            sign.transform.position=new Vector3(bounds.center.x,bounds.max.y+2.7f,bounds.max.z-.4f);
            sign.transform.rotation=Quaternion.Euler(0,180,0);sign.transform.localScale=Vector3.one;
            var label=sign.GetComponent<TextMeshPro>();label.fontSize=5;label.alignment=TextAlignmentOptions.Center;
            label.text=(state.phase==EpisodePhase.Veto?"POWER OF VETO":"HEAD OF HOUSEHOLD")+"\n"+EpisodeEngine.CompetitionCategory(state).ToUpperInvariant();
            var definition=CompetitionDefinitions.For(state);if(definition!=null)label.text+="\n"+definition.Title;
            label.color=state.phase==EpisodePhase.Veto?UiTheme.Award:UiTheme.Gold;
            TickCompetitionArena(); return competitionArenaStaging;
        }

        private void CreateCompetitionStationMesh()
        {
            const int edges=32; var vertices=new Vector3[edges+1]; var triangles=new int[edges*3];
            for(int i=0;i<edges;i++)
            {
                float angle=i*Mathf.PI*2/edges;vertices[i+1]=new Vector3(Mathf.Cos(angle)*.425f,0,Mathf.Sin(angle)*.425f);
                triangles[i*3]=0;triangles[i*3+1]=(i+1)%edges+1;triangles[i*3+2]=i+1;
            }
            competitionStationMesh=new Mesh{name="Competition station disc"};competitionStationMesh.vertices=vertices;
            competitionStationMesh.triangles=triangles;competitionStationMesh.RecalculateNormals();competitionStationMesh.RecalculateBounds();
        }

        private void AddCompetitionStationMarker(HouseInteractionAnchor anchor,string name)
        {
            var disc=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
            disc.transform.SetParent(anchor.transform,false);disc.transform.localPosition=Vector3.up*.018f;
            disc.GetComponent<MeshFilter>().sharedMesh=competitionStationMesh;
            var renderer=disc.GetComponent<MeshRenderer>();renderer.sharedMaterial=competitionArenaMaterial;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        }

        private void TickCompetitionArena()
        {
            if(!competitionArenaStaging)return;
            if(player==null || competitionPlayerStation==null || !competitionPlayerStation.isActiveAndEnabled
                || (competitionPlayerStation.Position-competitionPlayerStationPosition).sqrMagnitude>.0025f
                || Mathf.Abs(Mathf.DeltaAngle(competitionPlayerStation.Facing,competitionPlayerStationFacing))>.5f
                || !player.IsActivityMoveValid(competitionPlayerOwner))
            { message="Your competition route changed. Return to the briefing and try again.";CancelChallenge();return; }
            bool paused=competitionScreen!=null && competitionScreen.Paused;
            player.PauseActivityMove(competitionPlayerOwner,paused);
            bool castStaged=npcMeetings!=null && npcMeetings.HasCompetitionStage;
            if(castStaged && !npcMeetings.ValidateCompetitionStage(out var reason))
            { EndCompetitionCast();competitionAudienceStatus=reason+" The eligible field is still listed.";castStaged=false; }
            if(castStaged)npcMeetings.SetCompetitionStagePaused(paused);
            if(paused){competitionAssemblyDeadline+=Time.unscaledDeltaTime;return;}
            player.GetComponent<CharacterPresentation>()?.SetFacing(player.ActivityHasArrived(competitionPlayerOwner)?competitionPlayerStation.Facing:float.NaN);
            foreach(var pair in competitionArenaActors)
            {
                var npc=housemates.FirstOrDefault(n=>n!=null && n.Id==pair.Key);
                var visual=npc!=null ? npc.GetComponent<CharacterPresentation>() : null;
                if(visual==null)continue;
                bool arrived=npcMeetings.CompetitionActorArrived(pair.Key);
                visual.SetTalking(false);visual.SetArguing(false);visual.SetFacing(arrived?pair.Value.Facing:float.NaN);
                if(arrived && pair.Value.Seated)
                {
                    var seat=npc.GetComponent<HouseSeatPresentation>() ?? npc.gameObject.AddComponent<HouseSeatPresentation>();
                    if(seat.isActiveAndEnabled && !seat.Active)
                    {
                        string id=pair.Key;
                        seat.Begin(pair.Value,()=>competitionArenaStaging && npcMeetings!=null && npcMeetings.CompetitionActorArrived(id));
                        if(seat.Active && !competitionAudienceSeats.Contains(seat))competitionAudienceSeats.Add(seat);
                    }
                }
                else visual.SetSeated(false);
            }
            int arrivals=(player.ActivityHasArrived(competitionPlayerOwner)?1:0)+(castStaged?npcMeetings.CompetitionArrivals:0);
            int total=1+(castStaged?npcMeetings.CompetitionStageCount:0);
            competitionArenaStatus=arrivals+" / "+total+" houseguests in position. "+competitionAudienceStatus;
            if(Time.unscaledTime>competitionAssemblyDeadline && !CompetitionArenaReady)
            {
                if(!player.ActivityHasArrived(competitionPlayerOwner))
                {message="Your competition route could not finish. Your attempt has not started.";CancelChallenge();}
                else {EndCompetitionCast();competitionAudienceStatus="Some audience routes did not finish. The eligible field remains listed.";}
            }
        }

        private bool CompetitionArenaReady => competitionArenaStaging && player!=null && player.ActivityHasArrived(competitionPlayerOwner)
            && (npcMeetings==null || !npcMeetings.HasCompetitionStage || npcMeetings.CompetitionArrivals==npcMeetings.CompetitionStageCount);

        private void EndCompetitionCast()
        {
            foreach(var seat in competitionAudienceSeats)if(seat!=null)seat.End();
            competitionAudienceSeats.Clear(); npcMeetings?.EndCompetitionStage();
            foreach(var pair in competitionArenaActors)
            {
                var npc=housemates?.FirstOrDefault(n=>n!=null && n.Id==pair.Key);
                if(npc!=null)npc.GetComponent<CharacterPresentation>()?.SetFacing(float.NaN);
                // Authored furniture belongs to the scene. Only transient stage anchors are ours.
                if(pair.Value!=null && competitionArenaRoot!=null && pair.Value.transform.IsChildOf(competitionArenaRoot.transform))
                    Destroy(pair.Value.gameObject);
            }
            competitionArenaActors.Clear();
        }

        private void EndCompetitionArena()
        {
            competitionArenaStaging=false;EndCompetitionCast();
            if(player!=null && player.ReleaseActivityMove(competitionPlayerOwner))
                player.GetComponent<CharacterPresentation>()?.SetFacing(float.NaN);
            competitionPlayerStation=null;
            if(competitionArenaRoot!=null){Destroy(competitionArenaRoot);competitionArenaRoot=null;}
            if(competitionArenaMaterial!=null){Destroy(competitionArenaMaterial);competitionArenaMaterial=null;}
            if(competitionStationMesh!=null){Destroy(competitionStationMesh);competitionStationMesh=null;}
        }
    }
}
