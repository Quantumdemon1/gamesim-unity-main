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
        /// <summary>The arena's words are hidden under the board; physical instruments remain on the stage.</summary>
        private TextMeshPro competitionSign;
        private readonly List<Renderer> competitionArenaMarks=new List<Renderer>();
        private bool competitionOverlaysShown=true;
        private readonly Dictionary<string,HouseInteractionAnchor> competitionArenaActors=new Dictionary<string,HouseInteractionAnchor>();
        private readonly Dictionary<string,CompetitionApparatus> competitionInstruments=new Dictionary<string,CompetitionApparatus>();
        private sealed class CompetitionPose { public CharacterPresentation.Pose previous, written; }
        private readonly Dictionary<CharacterPresentation,CompetitionPose> competitionPoses=new Dictionary<CharacterPresentation,CompetitionPose>();
        private readonly List<HouseSeatPresentation> competitionAudienceSeats=new List<HouseSeatPresentation>();
        private readonly object competitionPlayerOwner=new object();
        private HouseInteractionAnchor competitionPlayerStation;
        private Vector3 competitionPlayerStationPosition;
        private float competitionPlayerStationFacing;
        private float competitionAssemblyDeadline;
        private string competitionArenaStatus, competitionAudienceStatus;
        private bool competitionArenaStaging;
        private bool competitionEffortFramed;

        private bool BeginCompetitionArena(EpisodeState state)
        {
            EndCompetitionArena();
            // The show's own stage comes first: a ceremony still holding its seats lets go for the arena.
            EndCeremonyStage();
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
                competitionPlayerStation=HouseInteractionAnchor.Create(competitionArenaRoot.transform,"competition-player","Yard",0,position,CompetitionRouteFacing(player.transform.position,position,filter),false);
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
            // A story scene lets its people go for the arena: the show's own stage comes first.
            npcMeetings?.EndSceneStage();
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
                                position,contestants.Contains(actor.id)?CompetitionRouteFacing(npc.transform.position,position,filter):Mathf.Atan2(bounds.center.x-position.x,bounds.center.z-position.z)*Mathf.Rad2Deg,false);
                            break;
                        }
                    if(anchor==null)continue;
                    ids.Add(actor.id); anchors.Add(anchor); used.Add(anchor.Approach); competitionArenaActors[actor.id]=anchor;
                }
                if(ids.Count>0 && npcMeetings.BeginCompetitionStage(ids,anchors,out var reason))
                    competitionAudienceStatus=ids.Count<state.Active.Count(c=>!c.isPlayer) ? "Some houseguests are unavailable; the eligible field remains listed." : "";
                else { EndCompetitionCast(); competitionAudienceStatus="Other houseguests could not reach their stage places; the eligible field is listed."; }
            }
            var definition=CompetitionDefinitions.For(state);
            Color accent=state.phase==EpisodePhase.Veto?UiTheme.Award:UiTheme.Gold;
            AddCompetitionInstrument(state.playerId,competitionPlayerStation,definition,EpisodeEngine.CompetitionCategory(state),accent);
            foreach(var pair in competitionArenaActors.Where(pair=>contestants.Contains(pair.Key)))
                AddCompetitionInstrument(pair.Key,pair.Value,definition,EpisodeEngine.CompetitionCategory(state),accent);
            var sign=new GameObject(CompetitionSignName,typeof(TextMeshPro));
            sign.transform.SetParent(competitionArenaRoot.transform,false);
            // Facing the deck, where the house and the competition camera look from - the backdrop's
            // lit face is toward -z. Turned half round, it read mirrored from every seat (MOCKUP-PASS-PLAN M2).
            sign.transform.rotation=Quaternion.identity;sign.transform.localScale=Vector3.one;
            var label=sign.GetComponent<TextMeshPro>();label.fontSize=5;label.alignment=TextAlignmentOptions.Center;
            // The final parts name their part where HEAD OF HOUSEHOLD stands (ENDGAME-PLAN F3).
            string award=state.phase==EpisodePhase.Veto?"POWER OF VETO"
                :IsFinalHoHPart(state.phase)?"FINAL HOH \u00b7 PART "+(state.phase==EpisodePhase.FinalHoHPart1?1:state.phase==EpisodePhase.FinalHoHPart2?2:3)
                :"HEAD OF HOUSEHOLD";
            label.text=award+"\n"+EpisodeEngine.CompetitionCategory(state).ToUpperInvariant();
            if(definition!=null)label.text+="\n"+definition.Title;
            label.color=state.phase==EpisodePhase.Veto?UiTheme.Award:UiTheme.Gold;
            // Lifted clear of whatever stands between the deck and the words: at 2.7 m the centre
            // gate's neon ran through them, and lifted over the gate alone they ran through the
            // entrance arch's lintel, 0.65 m higher than the gate's head and 0.2 m nearer. Measured
            // from the laid-out text - all its lines - so the lowest line's foot stands over the top.
            label.ForceMeshUpdate();
            var laid=label.textBounds;
            bool measured=label.textInfo.characterCount>0 && laid.size.y>0f && laid.size.y<10f;
            float foot=measured ? -laid.min.y : 1f;
            float halfWidth=measured && laid.size.x>0f ? laid.size.x*.5f : 3f;
            float signZ=bounds.max.z-.4f;
            // Never higher than a hand's breadth of sky over the wall behind it: whatever is left
            // standing on the back of the deck must not send the award into the dark over the yard.
            float lowest=Mathf.Min(CompetitionSignLineTop(bounds,signZ,halfWidth)+CompetitionSignClearance,
                bounds.max.y+CompetitionBackdropHeight+CompetitionSignOverWall);
            sign.transform.position=new Vector3(bounds.center.x,lowest+foot,signZ);
            competitionSign=label; competitionOverlaysShown=true;
            TickCompetitionArena(); return competitionArenaStaging;
        }

        /// <summary>The arena's sign of the award and the discipline, as the yard names it. A read, for tests.</summary>
        public const string CompetitionSignName="Award and discipline";

        /// <summary>The course's gates, as the set-piece pass names them (bb_set_comp_gate.py).</summary>
        public const string CompetitionGateName="bb_set_comp_gate";

        /// <summary>A gate's authored height over the deck, for a yard dressed without the course.</summary>
        public const float CompetitionGateHeight=2.6f;

        /// <summary>How far the sign's lowest line stands over the top of what stands between the deck and it.</summary>
        public const float CompetitionSignClearance=.25f;

        /// <summary>The backdrop's authored height over the deck (bb_set_comp_backdrop.py's HEIGHT).</summary>
        public const float CompetitionBackdropHeight=3f;

        /// <summary>
        /// The highest the sign's lowest line stands over the backdrop's top. The arch puts it at
        /// 0.5 m over the wall; a cap of 2 m lets the set grow without letting a stray prop send the
        /// sign off the top of the competition shot.
        /// </summary>
        public const float CompetitionSignOverWall=2f;

        /// <summary>
        /// The top of whatever stands between the deck and the sign's words: the course's centre
        /// gate, and the yard's entrance arch over it, whose lintel stands 0.65 m over the gate's
        /// head (3.25 m to its top) and 0.2 m nearer the deck - the sign stood clear of the gate and
        /// ran straight through the lintel (the show sweep's row 26). Every renderer on the back
        /// half of the deck, in front of the sign and under the words' width counts, up to a lamp's
        /// height; never the arena's own; and a gate's authored height over the deck at least, for
        /// a yard dressed without the course. Capped by the caller (<see cref="CompetitionSignOverWall"/>).
        /// </summary>
        private float CompetitionSignLineTop(Bounds deck,float signZ,float halfWidth)
        {
            float top=deck.max.y+CompetitionGateHeight;
            float left=deck.center.x-halfWidth,right=deck.center.x+halfWidth,ceiling=deck.max.y+CompetitionSignLineCeiling;
            var own=competitionArenaRoot!=null?competitionArenaRoot.transform:null;
            foreach(var root in gameObject.scene.GetRootGameObjects())
                foreach(var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    if(!renderer.enabled || renderer is ParticleSystemRenderer)continue;
                    if(own!=null && renderer.transform.IsChildOf(own))continue;
                    var b=renderer.bounds;
                    if(b.center.z<deck.center.z || b.min.z>=signZ)continue;
                    if(b.max.x<left || b.min.x>right)continue;
                    if(b.min.x<deck.min.x || b.max.x>deck.max.x || b.min.y>ceiling)continue;
                    top=Mathf.Max(top,b.max.y);
                }
            return top;
        }

        /// <summary>Metres over the deck past which a thing is hung, not standing, and the sign does not climb over it.</summary>
        public const float CompetitionSignLineCeiling=6f;

        /// <summary>
        /// The arena's own overlays - the award sign and instrument readouts - drawn only
        /// while nothing stands over the house (UI-UX-PASS-PLAN G0): the sign read through the
        /// competition board's glass on every game, straddling the seam between the briefing card
        /// and the play area. Physical apparatus stays on the stage while its words are gated.
        /// The word renderers
        /// go, not the objects: the sign keeps its place and its words for the tests that read them,
        /// and a board switched off for a frame - a capture of the yard - gets the yard back.
        /// </summary>
        private void SetCompetitionOverlaysVisible(bool visible)
        {
            if(competitionArenaRoot==null || competitionOverlaysShown==visible)return;
            competitionOverlaysShown=visible;
            if(competitionSign!=null)
            {
                var words=competitionSign.GetComponent<MeshRenderer>();
                if(words!=null)words.enabled=visible;
            }
            foreach(var mark in competitionArenaMarks)if(mark!=null)mark.enabled=visible;
        }

        /// <summary>Whether the arena's sign and instrument readouts are drawn this frame. A read, for tests.</summary>
        public bool CompetitionOverlaysShown=>competitionArenaRoot!=null && competitionOverlaysShown;

        private void AddCompetitionInstrument(string id,HouseInteractionAnchor anchor,CompetitionDefinition definition,string category,Color accent)
        {
            var instrument=CompetitionApparatus.Create(anchor.transform,definition,category,id,accent);
            competitionInstruments[id]=instrument;
            competitionArenaMarks.AddRange(instrument.OverlayRenderers);
            instrument.gameObject.SetActive(false); // Dress an occupied station, never a route somebody is still crossing.
        }

        private static float CompetitionRouteFacing(Vector3 from,Vector3 to,NavMeshQueryFilter filter)
        {
            var path=new NavMeshPath();Vector3 approach=to-from;
            if(NavMesh.CalculatePath(from,to,filter,path) && path.status==NavMeshPathStatus.PathComplete && path.corners.Length>1)
                approach=path.corners[path.corners.Length-1]-path.corners[path.corners.Length-2];
            return approach.sqrMagnitude>.001f?Mathf.Atan2(approach.x,approach.z)*Mathf.Rad2Deg:0;
        }

        private void SyncCompetitionInstruments()
        {
            foreach(var pair in competitionInstruments)
            {
                if(pair.Value==null)continue;
                bool mine=challengeOrigin!=null && pair.Key==challengeOrigin.playerId;
                bool arrived=mine?player!=null && player.ActivityHasArrived(competitionPlayerOwner)
                    :npcMeetings!=null && npcMeetings.CompetitionActorArrived(pair.Key);
                // Every approach route finishes before any solid scenery appears on the floor.
                pair.Value.gameObject.SetActive(arrived && CompetitionArenaReady);
                var body=mine?player?.gameObject:housemates.FirstOrDefault(npc=>npc!=null && npc.Id==pair.Key)?.gameObject;
                var capsule=body!=null?body.GetComponent<CapsuleCollider>():null;
                if(capsule!=null)pair.Value.FitActorClearance(capsule.radius*Mathf.Max(body.transform.lossyScale.x,body.transform.lossyScale.z),
                    pair.Value.transform.parent.InverseTransformPoint(body.transform.position));
                pair.Value.Sync(mine?challengeRun:null,competitionScreen!=null && competitionScreen.IsPlaying,
                    competitionScreen!=null && competitionScreen.IsPreviewing,competitionScreen!=null && competitionScreen.Paused,reducedMotion);
                body?.GetComponent<CompetitionInstrumentPose>()?.Bind(pair.Value,mine?challengeRun:null,
                    competitionScreen!=null && competitionScreen.IsPlaying,competitionScreen!=null && competitionScreen.Paused);
            }
        }

        private void CompetitionStance(CharacterPresentation visual,bool arrived,CompetitionApparatus instrument)
        {
            if(visual==null || !arrived || instrument==null || !visual.IsStill || !visual.CanAct(CharacterPresentation.BodyActivity.Posing))return;
            if(!competitionPoses.TryGetValue(visual,out var own))
            {
                if(visual.Activity!=CharacterPresentation.BodyActivity.None)return;
                own=new CompetitionPose{previous=visual.HeldPose};competitionPoses.Add(visual,own);
            }
            else if(visual.Activity!=CharacterPresentation.BodyActivity.Posing)return;
            bool effort=instrument.Instrument==CompetitionApparatus.Family.GripRig && competitionScreen!=null && competitionScreen.IsPlaying;
            if(visual.gameObject==player.gameObject)effort&=challengeRun!=null && challengeRun.Holding;
            own.written=effort?CharacterPresentation.Pose.PowerStance:CharacterPresentation.Pose.AtEase;
            visual.SetPose(own.written);visual.SetActivity(CharacterPresentation.BodyActivity.Posing);
            var contact=visual.GetComponent<CompetitionInstrumentPose>();
            if(contact==null)contact=visual.gameObject.AddComponent<CompetitionInstrumentPose>();
            bool mine=visual.gameObject==player.gameObject;
            contact.Bind(instrument,mine?challengeRun:null,competitionScreen!=null && competitionScreen.IsPlaying,competitionScreen!=null && competitionScreen.Paused);
        }

        private void ReleaseCompetitionStance(CharacterPresentation visual)
        {
            if(visual==null || !competitionPoses.TryGetValue(visual,out var own))return;
            visual.GetComponent<CompetitionInstrumentPose>()?.Release();
            if(visual.Activity==CharacterPresentation.BodyActivity.Posing && visual.HeldPose==own.written)
            {visual.SetActivity(CharacterPresentation.BodyActivity.None);visual.SetPose(own.previous);}
            competitionPoses.Remove(visual);
        }

        private void TickCompetitionArena()
        {
            // A finished ranked result on its plate is past every staging check: a houseguest
            // nudged off their mark in those 0.9 s must not throw the result away.
            if(!competitionArenaStaging || challengeFinishHold>0f)return;
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
            SyncCompetitionInstruments();
            if(paused){competitionAssemblyDeadline+=Time.unscaledDeltaTime;return;}
            player.GetComponent<CharacterPresentation>()?.SetFacing(player.ActivityHasArrived(competitionPlayerOwner)?competitionPlayerStation.Facing:float.NaN);
            if(challengeOrigin!=null && competitionInstruments.TryGetValue(challengeOrigin.playerId,out var playerInstrument))
            {
                CompetitionStance(player.GetComponent<CharacterPresentation>(),player.ActivityHasArrived(competitionPlayerOwner),playerInstrument);
                // Endurance's translucent play area was intended to show the effort. The assembly
                // keeps its wide shot; in play the camera can read the actor's hands and pressure rig.
                if(!competitionEffortFramed && CompetitionArenaReady && competitionScreen!=null && competitionScreen.IsPlaying
                    && playerInstrument.Instrument==CompetitionApparatus.Family.GripRig && cameraRig!=null && !cameraRig.ReducedMotion)
                {
                    competitionEffortFramed=true;
                    cameraRig.MoveTo(new HouseCameraRig.Shot
                    {
                        Focus=competitionPlayerStationPosition+Vector3.up*1.05f+competitionPlayerStation.transform.forward*.20f,
                        Distance=6.6f,Pitch=19f,Yaw=competitionPlayerStationFacing+35f,FieldOfView=40f,Seconds=CompetitionShotSeconds,
                    });
                }
            }
            foreach(var pair in competitionArenaActors)
            {
                var npc=housemates.FirstOrDefault(n=>n!=null && n.Id==pair.Key);
                var visual=npc!=null ? npc.GetComponent<CharacterPresentation>() : null;
                if(visual==null)continue;
                bool arrived=npcMeetings.CompetitionActorArrived(pair.Key);
                visual.SetTalking(false);visual.SetArguing(false);visual.SetFacing(arrived?pair.Value.Facing:float.NaN);
                if(competitionInstruments.TryGetValue(pair.Key,out var instrument))CompetitionStance(visual,arrived,instrument);
                if(arrived && pair.Value.Seated)
                {
                    // Explicitly: a missing component is Unity's fake null in the editor, which ?? keeps.
                    var seat=npc.GetComponent<HouseSeatPresentation>();
                    if(seat==null)seat=npc.gameObject.AddComponent<HouseSeatPresentation>();
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
                if(npc!=null){var visual=npc.GetComponent<CharacterPresentation>();ReleaseCompetitionStance(visual);visual?.SetFacing(float.NaN);}
                competitionInstruments.Remove(pair.Key);
                // Authored furniture belongs to the scene. Only transient stage anchors are ours.
                if(pair.Value!=null && competitionArenaRoot!=null && pair.Value.transform.IsChildOf(competitionArenaRoot.transform))
                    Destroy(pair.Value.gameObject);
            }
            competitionArenaActors.Clear();
        }

        private void EndCompetitionArena()
        {
            competitionArenaStaging=false;EndCompetitionCast();
            foreach(var visual in competitionPoses.Keys.ToArray())ReleaseCompetitionStance(visual);
            if(player!=null && player.ReleaseActivityMove(competitionPlayerOwner))
                player.GetComponent<CharacterPresentation>()?.SetFacing(float.NaN);
            competitionPlayerStation=null;
            competitionEffortFramed=false;
            competitionSign=null;competitionArenaMarks.Clear();competitionOverlaysShown=true;
            competitionInstruments.Clear();
            if(competitionArenaRoot!=null){Destroy(competitionArenaRoot);competitionArenaRoot=null;}
        }
    }
}
