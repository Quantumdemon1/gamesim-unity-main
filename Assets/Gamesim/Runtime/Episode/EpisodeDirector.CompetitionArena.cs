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
        private sealed class CompetitionBody
        {
            public GameObject root; public CapsuleCollider capsule; public CharacterPresentation visual;
            public CompetitionInstrumentPose contact;
            public bool arrived; public Vector3 arrivedPosition;
        }
        private readonly Dictionary<string,CompetitionBody> competitionBodies=new Dictionary<string,CompetitionBody>();
        private readonly Dictionary<string,CompetitionStageFootprint> competitionFootprints=new Dictionary<string,CompetitionStageFootprint>();
        private readonly Collider[] competitionPlacementHits=new Collider[256];
        private readonly HashSet<Transform> competitionRouteOwners=new HashSet<Transform>();
        private Collider competitionStageFloor;
        /// <summary>What is drawn standing on the deck, which a reservation clears as it clears colliders (read once per arena).</summary>
        private CompetitionStageScenery competitionScenery;
        private System.Func<CompetitionApparatus,bool> competitionFitGate;
        /// <summary>The complete reserved station envelopes, for world-fit diagnostics.</summary>
        public IReadOnlyDictionary<string,CompetitionStageFootprint> CompetitionFootprints=>competitionFootprints;
        private sealed class CompetitionPose { public CharacterPresentation.Pose previous, written; public CharacterPresentation.BodyActivity writtenActivity=CharacterPresentation.BodyActivity.Posing; public CompetitionInstrumentPose contact; }
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
        private bool competitionArenaWasReady;
        // The house approaches the deck from -Z. Matching rows face +Z, with the
        // instrument ahead and the native approach behind its actor. Path corner
        // directions remain the agent's concern, rather than rotating each whole
        // reservation into an irregular packing problem.
        private const float CompetitionContestantFacing=0f;
        // The contestants' second pass: every row again at this step, without the grid's own
        // places (each 1.8 m grid place is this many steps from the last).
        private const float CompetitionGapStep=.15f;
        private const int CompetitionGapStepsPerPlace=12;

        /// <summary>
        /// The one owner of what stands in the yard when (competition show, slice 1; competition yard,
        /// slice 2): everyday life with the competition equipment put away, or a competition's set
        /// deployed while the arena stands. Never saved.
        /// </summary>
        private readonly CompetitionYardDressing competitionYard=new CompetitionYardDressing();

        /// <summary>The yard's floor, found once.</summary>
        private BoxCollider competitionYardDeck;

        private BoxCollider CompetitionYardDeck()
        {
            // An explicit Unity null check: a destroyed floor reads as missing and is looked up again.
            if(competitionYardDeck==null)
                competitionYardDeck=gameObject.scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<BoxCollider>())
                    .FirstOrDefault(c=>c.name=="Competition yard floor");
            return competitionYardDeck;
        }

        /// <summary>
        /// Everyday life in the yard whenever no competition stands on it: the director's first
        /// projection, a load, a recovery, every projection after a commit (competition yard, slice 2).
        /// A competition's set is taken down only by the arena's own end.
        /// </summary>
        private void SettleCompetitionYard()
        {
            if(competitionArenaRoot!=null || competitionShow!=null)return;
            var deck=CompetitionYardDeck();
            if(deck!=null)competitionYard.Everyday(deck);
        }

        /// <summary>The id of the player's own station in the arena: the attempt's, or a show's when the player competed and has a station.</summary>
        private string CompetitionArenaPlayerId => challengeOrigin!=null ? challengeOrigin.playerId
            : competitionShow!=null && competitionPlayerStation!=null ? competitionShow.Plan.PlayerId : null;

        /// <summary>Whether the arena is paused: the player's game only; a show never pauses.</summary>
        private bool CompetitionArenaPaused => competitionShow==null && competitionScreen!=null && competitionScreen.Paused;

        /// <summary>Whether the game's preview is on: the player's game only.</summary>
        private bool CompetitionArenaPreviewing => competitionShow==null && competitionScreen!=null && competitionScreen.IsPreviewing;

        /// <summary>Whether this competitor is competing now: the game in play, or holding on in a show.</summary>
        private bool CompetitionArenaPlaying(string id) => competitionShow!=null ? CompetitionShowGripping(id)
            : competitionScreen!=null && competitionScreen.IsPlaying && !CompetitionGameDropped(id);

        /// <summary>
        /// The arena refused something it needs. The player's game says so and returns to its
        /// briefing, as it always has; a show plays its card instead, since its result is committed.
        /// </summary>
        private void CompetitionArenaRefused(string notice)
        {
            if(competitionShow!=null)
            {
                Debug.Log("[Gamesim competition show] "+notice+" The card plays.",this);
                FinishCompetitionShow();return;
            }
            message=notice;BeginSystemNotice();CancelChallenge();
        }

        private bool BeginCompetitionArena(EpisodeState state) => BeginCompetitionArena(state,null);

        /// <summary>
        /// Builds the arena: the player's own game when <paramref name="show"/> is null, as it always
        /// was; otherwise a show of a committed result, whose field is the committed standings, whose
        /// player takes a station only when they competed, and whose dropped competitors each get a
        /// place at the side to walk to (competition show, slice 1).
        /// </summary>
        private bool BeginCompetitionArena(EpisodeState state,CompetitionShowRun show)
        {
            // An exception part way through building the arena ends it: the yard goes back to everyday
            // life and nothing is left holding a body or a route (competition yard, slice 2).
            try { return BeginCompetitionArenaCore(state,show); }
            catch { EndCompetitionArena(); throw; }
        }

        private bool BeginCompetitionArenaCore(EpisodeState state,CompetitionShowRun show)
        {
            CancelNpcInvitation();
            EndCompetitionArena();
            // The show's own stage comes first: a ceremony still holding its seats lets go for the arena.
            EndCeremonyStage();
            competitionShow=show;
            npcMeetings?.ReleaseActivities();
            var floor=gameObject.scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<BoxCollider>())
                .FirstOrDefault(c=>c.name=="Competition yard floor");
            if(floor==null || player==null || player.Agent==null || !HouseRoomQuery.TryCreate(gameObject.scene,out var rooms,out _))
            { competitionArenaStatus="The competition floor is unavailable. Return when its route is ready."; competitionShow=null; return false; }
            var bounds=floor.bounds;
            var definition=show!=null ? show.Definition : CompetitionDefinitions.For(state);
            string category=show!=null ? show.Plan.Category : EpisodeEngine.CompetitionCategory(state);
            var phase=show!=null ? show.Plan.Phase : state.phase;
            var instrumentFamily=CompetitionApparatus.For(definition,category);
            competitionStageFloor=floor;
            competitionFitGate=ValidateCompetitionInstrumentFit;
            Physics.SyncTransforms();
            competitionArenaRoot=new GameObject("Competition stage runtime");
            competitionArenaRoot.transform.SetParent(floor.transform,false);
            var scale=floor.transform.lossyScale;
            competitionArenaRoot.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
            // The backyard changes for the competition: the course put away in everyday life is deployed
            // as its set, then the pieces in the field are put away before the scenery is read, so the
            // stations stand where they always have; everyday life comes back with the arena's end
            // (CompetitionYardDressing).
            competitionYard.Deploy(floor,competitionArenaRoot.transform,category);
            // The yard's collider-free set dressing - the entrance arch, the course's gates, signs and
            // backdrop, the display - is invisible to the physics below. Every station and audience
            // place clears it as well, with the same whole reservation (CompetitionStageScenery).
            competitionScenery=CompetitionStageScenery.Collect(floor,competitionArenaRoot.transform);
            // A ring tower is played on the yard's stacks: they stand again and carry the playable rings (EpisodeDirector.RingTower.cs).
            DeployRingTower(floor,definition);
            var filter=new NavMeshQueryFilter{agentTypeID=player.Agent.agentTypeID,areaMask=player.Agent.areaMask};
            var playerCapsule=player.GetComponent<CapsuleCollider>();
            float layoutRadius=Mathf.Max(player.Agent.radius,playerCapsule!=null?playerCapsule.radius:0);
            foreach(var npc in housemates)
                if(npc!=null && state.Active.Any(actor=>actor.id==npc.Id))
                {
                    var npcCapsule=npc.GetComponent<CapsuleCollider>();
                    if(npcCapsule!=null)layoutRadius=Mathf.Max(layoutRadius,npcCapsule.radius);
                }
            float layoutArrival=Mathf.Max(.25f,player.Agent.stoppingDistance+.1f);
            // Use the deepest family for every game so a later instrument can reuse
            // the same aligned rows. The old 1.6 m stride was shorter than both a
            // console's 1.95 m and a grip's 2.15 m reservation; neighbouring rows
            // excluded each other and left unusable strips between the set pieces.
            var deepest=CompetitionStageFootprint.Station(CompetitionApparatus.Family.GripRig,
                Vector3.zero,CompetitionContestantFacing,layoutRadius,player.Agent.height,layoutArrival);
            float rowStride=deepest.HalfSize.z*2f+.06f;
            // Reserve the actual forward reach, then .15 m for the back wall and
            // separation. The wall extends .09 m into this deck. All real solids
            // still pass the complete-envelope query; this is only a grid boundary.
            float backRow=bounds.max.z-(deepest.Center.z+deepest.HalfSize.z)-.15f;
            // A row at10.91 m filled the doorway's exit: stopped Quinn/Riley held
            // Sam/Taylor against their native routes. Leave the entrance band open.
            // At default dimensions these complete, separated rows end at11.59 m,
            // while retaining capacity for the validated16-person stored field.
            float frontLimit=Mathf.Max(bounds.min.z+1.5f,bounds.min.z+layoutRadius+layoutArrival+.14f);
            var candidates=new List<Vector3>();
            var gaps=new List<Vector3>();
            for(float z=backRow;z>frontLimit;z-=rowStride)
            {
                for(float x=bounds.min.x+2f;x<bounds.max.x-2f;x+=1.8f)
                    candidates.Add(new Vector3(x,bounds.max.y,z));
                for(int step=1;bounds.min.x+2f+step*CompetitionGapStep<bounds.max.x-2f;step++)
                    if(step%CompetitionGapStepsPerPlace!=0)gaps.Add(new Vector3(bounds.min.x+2f+step*CompetitionGapStep,bounds.max.y,z));
            }
            // Fill the far rows first so stopped arrivals do not occupy the entrance
            // while the rest of the field is still walking through it. Every candidate
            // retains its real floor/path and complete apparatus-clearance checks.
            // A contestant takes a grid place while one is clear. Only then does it look
            // in the gaps the 1.8 m grid steps over, every row again at a fine step in
            // the same order: the room left beside a set piece, or beside a houseguest
            // standing where a grid place would be (a body is no route owner while the
            // cast is placed, and a station cannot be leased on top of it). An entrant
            // with no clear place in either pass logs an error rather than overlapping.
            var contestantCandidates=candidates.OrderByDescending(p=>p.z)
                .ThenBy(p=>Mathf.Abs(p.x-bounds.center.x)).ThenBy(p=>p.x)
                .Concat(gaps.OrderByDescending(p=>p.z).ThenBy(p=>Mathf.Abs(p.x-bounds.center.x)).ThenBy(p=>p.x)).ToArray();
            // The player uses a real complete path under an owner token. Ordinary input remains
            // locked; neither the navigation root nor its collider is teleported or reconfigured.
            // A show stations the player only when they competed; one watching stays where they are.
            bool stationPlayer=show==null || show.Plan.PlayerInField;
            if(stationPlayer)
            foreach(var wanted in RingTowerPlayerCandidates(contestantCandidates))
            {
                if(!rooms.TrySampleFloor(wanted,player.Agent.radius,filter,.25f,out var position,out var room)
                    || room!="Yard" || !rooms.HasCapsuleClearance(position,player.Agent.radius,player.Agent.height,player.transform))continue;
                float facing=CompetitionContestantFacing;
                float radius=Mathf.Max(player.Agent.radius,playerCapsule!=null?playerCapsule.radius:0);
                // At the ring tower the player stands at the stacks with no instrument of their own: a standing reservation.
                var footprint=competitionRingTower!=null
                    ?CompetitionStageFootprint.Actor(position,radius,player.Agent.height,Mathf.Max(.25f,player.Agent.stoppingDistance+.1f))
                    :CompetitionStageFootprint.Station(instrumentFamily,position,facing,radius,player.Agent.height,
                    Mathf.Max(.25f,player.Agent.stoppingDistance+.1f));
                if(!FitsCompetitionFootprint(footprint,floor,player.transform) || !player.TryBeginActivityMove(competitionPlayerOwner,position,out _))continue;
                competitionRouteOwners.Add(player.transform);
                competitionFootprints[state.playerId]=footprint;
                competitionPlayerStation=HouseInteractionAnchor.Create(competitionArenaRoot.transform,"competition-player","Yard",0,position,facing,false);
                competitionPlayerStationPosition=position; competitionPlayerStationFacing=competitionPlayerStation.Facing;
                break;
            }
            // The player's game cannot start without their station. A show goes on without it: the
            // player watches from where they stand and the rest of the field still competes.
            if(competitionPlayerStation==null && show==null)
            { EndCompetitionArena(); competitionArenaStatus="Your competition station has no clear route. Try again after the path is clear."; return false; }
            competitionArenaStaging=true; competitionAssemblyDeadline=Time.unscaledTime+30;
            CacheCompetitionBody(state.playerId,player.gameObject);
            var contestants=show!=null ? new HashSet<string>(show.Plan.Standings)
                : new HashSet<string>(EpisodeEngine.CompetitionPlayers(state).Select(c=>c.id));
            var ids=new List<string>(); var anchors=new List<HouseInteractionAnchor>();
            var used=new List<Vector3>();
            if(competitionPlayerStation!=null)used.Add(competitionPlayerStationPosition);
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
                    if(capsule==null || (npc.GetComponent<HouseSeatPresentation>()?.Active ?? false))
                    {
                        Debug.Log("[Gamesim competition stage] unavailable "+actor.id+": capsule="+(capsule!=null)
                            +", seated="+(npc!=null && (npc.GetComponent<HouseSeatPresentation>()?.Active ?? false)),this);
                        continue;
                    }
                    CacheCompetitionBody(actor.id,npc.gameObject);
                    HouseInteractionAnchor anchor=null;
                    CompetitionStageFootprint footprint=default;
                    int floorMisses=0,occupiedMisses=0,envelopeMisses=0;
                    Vector3 lastCandidate=default;CompetitionStageFootprint lastEnvelope=default;
                    var existingSeat=npc.GetComponent<HouseSeatPresentation>();
                    if(!contestants.Contains(actor.id) && (existingSeat==null || existingSeat.isActiveAndEnabled))
                        foreach(var seat in seats.Where(a=>!usedSeats.Contains(a)))
                            if(rooms.TrySampleFloor(seat.Approach,capsule.radius,filter,.25f,out var approach,out var room)
                                && room=="Yard" && (approach-seat.Approach).sqrMagnitude<.09f
                                && used.All(p=>(p-approach).sqrMagnitude>=1.6f)
                                && rooms.HasCapsuleClearance(approach,capsule.radius,capsule.height,npc.transform))
                            {
                                // The authored seat owns its furniture contact. Reserve both its
                                // approach and seated body against the other stage participants.
                                var approachFootprint=CompetitionStageFootprint.Actor(approach,capsule.radius,capsule.height);
                                var seatedFootprint=CompetitionStageFootprint.Actor(seat.Position,capsule.radius,capsule.height);
                                if(!ClearsCompetitionNeighbours(approachFootprint) || !ClearsCompetitionNeighbours(seatedFootprint))continue;
                                anchor=seat;footprint=seatedFootprint;usedSeats.Add(seat);break;
                            }
                    var audiencePreferred=new Vector3(bounds.min.x+2f,bounds.max.y,bounds.center.z);
                    // A tower show's winner takes the place at the stacks when the player has none (EpisodeDirector.TowerShow.cs).
                    bool atStacks=CompetitionTowerShowAtTheStacks(show,actor.id);
                    IEnumerable<Vector3> actorCandidates=atStacks ? RingTowerPlayerCandidates(contestantCandidates)
                        :contestants.Contains(actor.id) ? RingTowerFieldCandidates(contestantCandidates,bounds)
                        :candidates.OrderBy(p=>(p-audiencePreferred).sqrMagnitude);
                    if(anchor==null)
                        foreach(var wanted in actorCandidates)
                        {
                            if(!rooms.TrySampleFloor(wanted,capsule.radius,filter,.25f,out var position,out var room) || room!="Yard")
                            {floorMisses++;continue;}
                            if(used.Any(p=>(p-position).sqrMagnitude<1.6f)
                                || !rooms.HasCapsuleClearance(position,capsule.radius,capsule.height,npc.transform))
                            {occupiedMisses++;continue;}
                            float facing=contestants.Contains(actor.id)?CompetitionContestantFacing
                                :Mathf.Atan2(bounds.center.x-position.x,bounds.center.z-position.z)*Mathf.Rad2Deg;
                            footprint=contestants.Contains(actor.id) && !atStacks
                                ?CompetitionStageFootprint.Station(instrumentFamily,position,facing,capsule.radius,capsule.height)
                                :CompetitionStageFootprint.Actor(position,capsule.radius,capsule.height);
                            if(!FitsCompetitionFootprint(footprint,floor,npc.transform))
                            {envelopeMisses++;lastCandidate=position;lastEnvelope=footprint;continue;}
                            anchor=HouseInteractionAnchor.Create(competitionArenaRoot.transform,
                                contestants.Contains(actor.id)?"competition-contestant":"competition-audience","Yard",ids.Count,
                                position,facing,false);
                            break;
                        }
                    if(anchor==null)
                    {
                        string refusal="[Gamesim competition stage] no station for "+actor.id+" family="+instrumentFamily
                            +" from="+npc.transform.position.ToString("F3")+" candidates="+(contestants.Contains(actor.id)?contestantCandidates.Length:candidates.Count)
                            +" rowStride="+rowStride.ToString("F3")+" layoutRadius="+layoutRadius.ToString("F3")
                            +" floor="+floorMisses+" occupied="+occupiedMisses+" envelope="+envelopeMisses
                            +" last="+lastCandidate.ToString("F3")+" deck="+lastEnvelope.FitsOn(floor.bounds)
                            +" neighbour="+ClearsCompetitionNeighbours(lastEnvelope)
                            +" blockers="+CompetitionStaticBlockers(lastEnvelope,npc.transform)
                            +" scenery="+(competitionScenery!=null?competitionScenery.Blockers(lastEnvelope):"none")
                            +" rejected="+CompetitionRejectedCandidates(contestants.Contains(actor.id)?contestantCandidates:(IReadOnlyList<Vector3>)candidates,used,npc,capsule,floor,rooms,filter,instrumentFamily,contestants.Contains(actor.id));
                        // An entrant the yard cannot seat is an error, not a quiet gap: every clear place is
                        // taken, and the alternative - a station through a set piece or a neighbour - is
                        // exactly what the reservation exists to refuse. The audience may stand down quietly.
                        if(contestants.Contains(actor.id))Debug.LogError(refusal,this);else Debug.Log(refusal,this);
                        continue;
                    }
                    ids.Add(actor.id); anchors.Add(anchor); used.Add(anchor.Approach); competitionArenaActors[actor.id]=anchor;
                    competitionFootprints[actor.id]=footprint;
                    if(anchor.Seated)competitionFootprints[actor.id+":approach"]=CompetitionStageFootprint.Actor(anchor.Approach,capsule.radius,capsule.height);
                }
                if(show!=null)ReserveCompetitionShowMarks(show,state,ids,rooms,filter,floor,candidates.Concat(gaps),used);
                string stageReason=null;
                if(ids.Count>0 && npcMeetings.BeginCompetitionStage(ids,anchors,out stageReason))
                {
                    foreach(var id in ids)if(competitionBodies.TryGetValue(id,out var body) && body.root!=null)
                        competitionRouteOwners.Add(body.root.transform);
                    competitionAudienceStatus=ids.Count<state.Active.Count(c=>!c.isPlayer) ? "Some houseguests are unavailable; the eligible field remains listed." : "";
                }
                else
                {
                    Debug.Log("[Gamesim competition stage] native lease request failed: "+stageReason+". "+CompetitionStageDiagnostic(),this);
                    EndCompetitionCast(); competitionAudienceStatus="Other houseguests could not reach their stage places; the eligible field is listed.";
                }
            }
            Color accent=phase==EpisodePhase.Veto?UiTheme.Award:UiTheme.Gold;
            // The ring tower's equipment is the yard's stacks: nobody gets a console, and nobody pretends to solve.
            if(!RingTowerArena(definition))
            {
                if(competitionPlayerStation!=null)AddCompetitionInstrument(state.playerId,competitionPlayerStation,definition,category,accent);
                foreach(var pair in competitionArenaActors.Where(pair=>contestants.Contains(pair.Key)))
                    AddCompetitionInstrument(pair.Key,pair.Value,definition,category,accent);
            }
            var sign=new GameObject(CompetitionSignName,typeof(TextMeshPro));
            sign.transform.SetParent(competitionArenaRoot.transform,false);
            // Facing the deck, where the house and the competition camera look from - the backdrop's
            // lit face is toward -z. Turned half round, it read mirrored from every seat (MOCKUP-PASS-PLAN M2).
            sign.transform.rotation=Quaternion.identity;sign.transform.localScale=Vector3.one;
            var label=sign.GetComponent<TextMeshPro>();label.fontSize=5;label.alignment=TextAlignmentOptions.Center;
            // The final parts name their part where HEAD OF HOUSEHOLD stands (ENDGAME-PLAN F3).
            string award=phase==EpisodePhase.Veto?"POWER OF VETO"
                :IsFinalHoHPart(phase)?"FINAL HOH \u00b7 PART "+(phase==EpisodePhase.FinalHoHPart1?1:phase==EpisodePhase.FinalHoHPart2?2:3)
                :"HEAD OF HOUSEHOLD";
            label.text=award+"\n"+category.ToUpperInvariant();
            if(definition!=null)label.text+="\n"+definition.Title;
            label.color=phase==EpisodePhase.Veto?UiTheme.Award:UiTheme.Gold;
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
            // The stage on the yard's layer as it is built (ROOM-SCOPE-PLAN RS2a): the sign now, each
            // instrument the frame it is first drawn - they wait inactive until their station is taken.
            HouseRoomScope.PlaceDressing(competitionArenaRoot.transform);
            TickCompetitionArena(); return competitionArenaStaging;
        }

        /// <summary>
        /// A show's places at the side of the yard (competition show, slice 1): one for every
        /// stationed competitor who will let go, nearest the yard's west edge first, each a whole
        /// standing reservation clear of every station, audience place and set piece as the audience's
        /// are. A competitor with no place left stands by their rig when they let go.
        /// </summary>
        private void ReserveCompetitionShowSideMarks(CompetitionShowRun show,EpisodeState state,IReadOnlyCollection<string> staged,
            HouseRoomQuery rooms,NavMeshQueryFilter filter,Collider floor,IEnumerable<Vector3> places,List<Vector3> used)
        {
            var bounds=floor.bounds;
            var preferred=new Vector3(bounds.min.x+1.6f,bounds.max.y,bounds.center.z);
            var ordered=places.OrderBy(p=>(p-preferred).sqrMagnitude).ToArray();
            var playerCapsule=player.GetComponent<CapsuleCollider>();
            foreach(string id in show.Plan.DropOrder)
            {
                Transform self;float radius,height;
                if(id==state.playerId)
                {
                    if(competitionPlayerStation==null)continue;
                    self=player.transform;radius=Mathf.Max(player.Agent.radius,playerCapsule!=null?playerCapsule.radius:0);height=player.Agent.height;
                }
                else
                {
                    if(!staged.Contains(id) || !competitionBodies.TryGetValue(id,out var body) || body.root==null || body.capsule==null)continue;
                    self=body.root.transform;radius=body.capsule.radius;height=body.capsule.height;
                }
                foreach(var wanted in ordered)
                {
                    if(!rooms.TrySampleFloor(wanted,radius,filter,.25f,out var position,out var room) || room!="Yard")continue;
                    if(used.Any(p=>(p-position).sqrMagnitude<1.6f) || !rooms.HasCapsuleClearance(position,radius,height,self))continue;
                    var footprint=CompetitionStageFootprint.Actor(position,radius,height);
                    if(!FitsCompetitionFootprint(footprint,floor,self))continue;
                    float facing=Mathf.Atan2(bounds.center.x-position.x,bounds.center.z-position.z)*Mathf.Rad2Deg;
                    var mark=HouseInteractionAnchor.Create(competitionArenaRoot.transform,"competition-side","Yard",100+show.SideMarks.Count,position,facing,false);
                    show.SideMarks[id]=mark;used.Add(mark.Approach);competitionFootprints[id+":side"]=footprint;
                    break;
                }
            }
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
            // Owning/fitting scenery does not require humanoid action capability. Primitive
            // bodies also need a late clearance gate; stance and hand borrowing remain separate.
            if(competitionBodies.TryGetValue(id,out var body) && body.root!=null)
            {
                if(body.contact==null)body.contact=body.root.AddComponent<CompetitionInstrumentPose>();
                body.contact.SetFitGate(competitionFitGate);
            }
            competitionArenaMarks.AddRange(instrument.OverlayRenderers);
            instrument.gameObject.SetActive(false); // Dress an occupied station, never a route somebody is still crossing.
        }

        private bool ClearsCompetitionNeighbours(CompetitionStageFootprint footprint)
        {
            foreach(var reserved in competitionFootprints.Values)if(footprint.Overlaps(reserved))return false;
            return true;
        }

        private bool FitsCompetitionFootprint(CompetitionStageFootprint footprint,Collider floor,Transform prospectiveSelf=null)
            =>footprint.FitsOn(floor.bounds) && ClearsCompetitionNeighbours(footprint)
                && (competitionScenery==null || competitionScenery.Clears(footprint))
                && footprint.HasStaticClearance(gameObject.scene.GetPhysicsScene(),floor,competitionPlacementHits,competitionRouteOwners,prospectiveSelf);

        private string CompetitionRejectedCandidates(IReadOnlyList<Vector3> candidates,IReadOnlyList<Vector3> used,HouseNpc npc,
            CapsuleCollider capsule,Collider floor,HouseRoomQuery rooms,NavMeshQueryFilter filter,CompetitionApparatus.Family family,bool contestant)
        {
            var refused=new List<string>();
            foreach(var wanted in candidates.Take(128))
            {
                if(!rooms.TrySampleFloor(wanted,capsule.radius,filter,.25f,out var position,out var room) || room!="Yard")
                {refused.Add(wanted.ToString("F3")+":floor:"+rooms.LastFailure);continue;}
                bool spacing=used.All(p=>(p-position).sqrMagnitude>=1.6f);
                bool bodyClear=rooms.HasCapsuleClearance(position,capsule.radius,capsule.height,npc.transform);
                string bodyReason=bodyClear?"clear":rooms.LastFailure;
                var footprint=contestant?CompetitionStageFootprint.Station(family,position,CompetitionContestantFacing,capsule.radius,capsule.height)
                    :CompetitionStageFootprint.Actor(position,capsule.radius,capsule.height);
                bool staticClear=footprint.HasStaticClearance(gameObject.scene.GetPhysicsScene(),floor,competitionPlacementHits,competitionRouteOwners,npc.transform);
                refused.Add(wanted.ToString("F3")+"->"+position.ToString("F3")+":spacing="+spacing+",body="+bodyReason
                    +",deck="+footprint.FitsOn(floor.bounds)+",neighbours="+ClearsCompetitionNeighbours(footprint)
                    +",static="+staticClear+",blockers="+(staticClear?"none":CompetitionStaticBlockers(footprint,npc.transform))
                    +",scenery="+(competitionScenery!=null?competitionScenery.Blockers(footprint):"none"));
            }
            return (candidates.Count>128?"first128 only; ":"")+string.Join("; ",refused);
        }

        private bool ValidateCompetitionInstrumentFit(CompetitionApparatus instrument)
        {
            if(instrument==null || !competitionArenaStaging || competitionStageFloor==null
                || !competitionInstruments.TryGetValue(instrument.ActorId,out var owned) || owned!=instrument
                || !competitionBodies.TryGetValue(instrument.ActorId,out var body) || body.root==null)return false;
            // A completed ranked attempt owns its finish plate and imminent commit. Scenery
            // can stand down while that plate holds, but clearance cannot discard the result.
            if(challengeFinishHold>0f || !challengePractice && challengeRun!=null && challengeRun.Finished)
            {instrument.gameObject.SetActive(false);return false;}
            bool mine=instrument.ActorId==CompetitionArenaPlayerId;
            bool arrived=CompetitionBodyOccupiesStation(instrument.ActorId,body,mine);
            if(!arrived || !CompetitionPresentationReady || body.visual!=null && (body.visual.IsBodyAssembling || body.visual.IsChangingOutfit))
            {instrument.gameObject.SetActive(false);return false;}
            Physics.SyncTransforms();
            bool routeOwned=competitionRouteOwners.Contains(body.root.transform);
            bool reserved=competitionFootprints.TryGetValue(instrument.ActorId,out var footprint);
            bool fitted=reserved && instrument.Fits(footprint);
            bool clear=reserved && footprint.HasStaticClearance(gameObject.scene.GetPhysicsScene(),competitionStageFloor,competitionPlacementHits,competitionRouteOwners);
            if(!routeOwned || !reserved || !fitted || !clear)
            {
                var outside=reserved?instrument.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(mesh=>!footprint.Contains(mesh)):null;
                Debug.Log("[Gamesim competition stage] fitted station refused actor="+instrument.ActorId+" family="+instrument.Instrument
                    +" owned="+routeOwned+" reserved="+reserved+" fitted="+fitted+" clear="+clear
                    +" mesh="+(outside!=null?outside.name:"none")+" actor="+body.root.transform.position.ToString("F3")
                    +" instrumentOffset="+instrument.transform.localPosition.ToString("F3")
                    +" corner="+(outside!=null?CompetitionOutsideCorner(footprint,outside):"none")
                    +" blockers="+(reserved?CompetitionStaticBlockers(footprint):"no reservation")+". "+CompetitionStageDiagnostic(),this);
                // A show's result is committed: one rig it cannot stand goes, and the show goes on.
                if(competitionShow!=null){instrument.gameObject.SetActive(false);return false;}
                CompetitionArenaRefused("An instrument's reserved place is obstructed. You are back at the briefing: try again.");return false;
            }
            instrument.gameObject.SetActive(true);return true;
        }

        private bool CompetitionBodyOccupiesStation(string id,CompetitionBody body,bool mine)
        {
            if(mine)return player!=null && player.ActivityHasArrived(competitionPlayerOwner);
            if(CompetitionArenaPaused)return PausedCompetitionArrival(body);
            body.arrived=npcMeetings!=null && npcMeetings.CompetitionActorArrived(id);
            if(body.arrived && body.root!=null)body.arrivedPosition=body.root.transform.position;
            return body.arrived;
        }

        // Failure-only native evidence is recorded before cancellation releases the routes.
        // The public message remains short; these reads never change actor roots or season state.
        private string CompetitionStageDiagnostic()
        {
            return "playerArrived="+(player!=null && player.ActivityHasArrived(competitionPlayerOwner))
                +" native="+(npcMeetings!=null?npcMeetings.CompetitionArrivals+"/"+npcMeetings.CompetitionStageCount:"missing")
                +" actors="+string.Join("; ",competitionArenaActors.Select(pair=>
                {
                    if(!competitionBodies.TryGetValue(pair.Key,out var body) || body.root==null)return pair.Key+":missing";
                    var motion=body.root.GetComponent<HouseNpcMotion>();
                    return pair.Key+" "+(motion!=null?motion.State.ToString():"no motion")+" at="+body.root.transform.position.ToString("F3")
                        +" destination="+(motion!=null?motion.ReservedDestination.ToString("F3"):"none")
                        +" arrival="+(motion!=null?motion.ArrivalFailure??"proved":"no motion")
                        +(motion!=null && motion.State==HouseNpcMotionState.Walking?CompetitionWalkerDiagnostic(body.root):"");
                }));
        }

        private static string CompetitionOutsideCorner(CompetitionStageFootprint footprint,MeshFilter mesh)
        {
            if(mesh.sharedMesh==null)return "no mesh";
            if(footprint.TryOutsideCorner(mesh,out var world,out var local,out var excess))
                return "world="+world.ToString("F9")+" local="+local.ToString("F9")+" limit="+footprint.ContainmentLimit.ToString("F9")
                    +" excess="+excess.ToString("F9")+" reservedCenter="+footprint.Center.ToString("F9")
                    +" meshScale="+mesh.transform.lossyScale.ToString("F9")
                    +" anchorScale="+mesh.transform.parent.parent.lossyScale.ToString("F9");
            return "all corners inside";
        }

        private string CompetitionWalkerDiagnostic(GameObject body)
        {
            var agent=body.GetComponent<NavMeshAgent>();
            string route=" agent="+(agent!=null && agent.enabled && agent.isOnNavMesh);
            if(agent!=null && agent.enabled && agent.isOnNavMesh)
                route+=" stopped="+agent.isStopped+" speed="+agent.speed.ToString("F3")
                    +" desired="+agent.desiredVelocity.ToString("F3")+" velocity="+agent.velocity.ToString("F3")
                    +" pending="+agent.pathPending+" status="+agent.pathStatus+" remaining="+agent.remainingDistance.ToString("F3")
                    +" corners="+string.Join(",",agent.path.corners.Take(4).Select(p=>p.ToString("F3")));
            var nearby=housemates.Where(npc=>npc!=null && npc.gameObject!=body && (npc.transform.position-body.transform.position).sqrMagnitude<2.25f)
                .Select(npc=>
                {
                    var motion=npc.GetComponent<HouseNpcMotion>();var otherAgent=npc.GetComponent<NavMeshAgent>();
                    return npc.Id+"@"+npc.transform.position.ToString("F3")+" "+(motion!=null?motion.State.ToString():"no motion")
                        +" lease="+(motion!=null?motion.LeaseId??"none":"none")
                        +" stopped="+(otherAgent!=null && otherAgent.enabled && otherAgent.isOnNavMesh?otherAgent.isStopped.ToString():"no agent");
                });
            int count=gameObject.scene.GetPhysicsScene().OverlapSphere(body.transform.position+Vector3.up*.9f,1.5f,
                competitionPlacementHits,~0,QueryTriggerInteraction.Ignore);
            var solids=new List<string>();
            for(int i=0;i<count;i++)
            {
                var collider=competitionPlacementHits[i];
                if(collider==null || collider==competitionStageFloor || collider.transform.IsChildOf(body.transform))continue;
                solids.Add(collider.name+"@"+collider.transform.position.ToString("F3"));
            }
            return route+" nearbyActors=["+string.Join(";",nearby)+"] nearbySolids=["
                +(count==competitionPlacementHits.Length?"query overflow;":"")+string.Join(";",solids)+"]";
        }

        private string CompetitionStaticBlockers(CompetitionStageFootprint footprint,Transform prospectiveSelf=null)
        {
            int count=gameObject.scene.GetPhysicsScene().OverlapBox(footprint.Center,footprint.HalfSize,competitionPlacementHits,
                footprint.Rotation,~0,QueryTriggerInteraction.Ignore);
            var blocked=new List<string>();
            for(int i=0;i<count;i++)
            {
                var collider=competitionPlacementHits[i];if(collider==null || collider==competitionStageFloor)continue;
                bool owned=false;
                for(var at=collider.transform;at!=null;at=at.parent)
                    if(at==prospectiveSelf || competitionRouteOwners.Contains(at)){owned=true;break;}
                if(!owned)blocked.Add(collider.name+"@"+collider.transform.position.ToString("F3"));
            }
            return (count==competitionPlacementHits.Length?"query overflow; ":"")+string.Join(",",blocked);
        }

        private bool PausedCompetitionArrival(CompetitionBody body)
            =>body!=null && body.root!=null && body.arrived && competitionRouteOwners.Contains(body.root.transform)
                && (body.root.transform.position-body.arrivedPosition).sqrMagnitude<=.0025f;

        // Native arrivals still exclusively gate GO. Paused presentation may retain only a
        // field whose native arrivals were proven before pause and whose owned roots stay put.
        private bool CompetitionPresentationReady
        {
            get
            {
                if(CompetitionArenaReady)return true;
                // A show's hold, once begun, keeps its rigs while the dropped walk aside and latecomers arrive.
                if(competitionShow!=null)return competitionShow.HoldBegun;
                if(!competitionArenaStaging || !competitionArenaWasReady || competitionScreen==null || !competitionScreen.Paused
                    || player==null || !player.ActivityHasArrived(competitionPlayerOwner))return false;
                foreach(var id in competitionArenaActors.Keys)
                    if(!competitionBodies.TryGetValue(id,out var body) || !PausedCompetitionArrival(body))return false;
                return true;
            }
        }

        private void SyncCompetitionInstruments()
        {
            foreach(var pair in competitionInstruments)
            {
                // A show's released competitor has let go: their rig reads out where they left it.
                // So has one who let go around the player in their own game (EpisodeDirector.CompetitionDropouts.cs).
                if(CompetitionShowReleased(pair.Key) || CompetitionGameDropped(pair.Key))continue;
                SyncCompetitionInstrument(pair.Key,pair.Value);
            }
        }

        private void CacheCompetitionBody(string id,GameObject body)
        {
            competitionBodies[id]=new CompetitionBody{root=body,capsule=body.GetComponent<CapsuleCollider>(),
                visual=body.GetComponent<CharacterPresentation>(),contact=body.GetComponent<CompetitionInstrumentPose>()};
        }

        // Only the player's attempt changes in TickMiniGame. NPC stations already synchronized
        // with their leases in TickCompetitionArena; a second full field pass serves no new state.
        private void SyncCompetitionPlayerInstrument()
        {
            if(challengeOrigin!=null && competitionInstruments.TryGetValue(challengeOrigin.playerId,out var instrument))
                SyncCompetitionInstrument(challengeOrigin.playerId,instrument);
        }

        private void SyncCompetitionInstrument(string id,CompetitionApparatus instrument)
        {
                if(instrument==null || !competitionBodies.TryGetValue(id,out var body) || body.root==null)return;
                bool mine=id==CompetitionArenaPlayerId;
                bool arrived=CompetitionBodyOccupiesStation(id,body,mine);
                // Every approach route finishes before any solid scenery appears on the floor.
                // The late fitted-geometry gate alone may activate scenery. This pass can only
                // hide it when a native arrival or the complete field's route proof is lost.
                if(!arrived || !CompetitionPresentationReady)instrument.gameObject.SetActive(false);
                if(body.capsule!=null)instrument.FitActorClearance(body.capsule.radius*Mathf.Max(body.root.transform.lossyScale.x,body.root.transform.lossyScale.z),
                    instrument.transform.parent.InverseTransformPoint(body.root.transform.position));
                instrument.Sync(mine?challengeRun:null,CompetitionArenaPlaying(id),CompetitionArenaPreviewing,CompetitionArenaPaused,reducedMotion);
                body.contact?.Bind(instrument,mine?challengeRun:null,CompetitionArenaPlaying(id),CompetitionArenaPaused,arrived);
                if(competitionShow!=null)
                {
                    // The show's effort: the meter drains and the body strains as the hold goes on.
                    float strain=CompetitionShowStrain(id);
                    if(competitionShow.HoldBegun)instrument.ShowHold(1f-strain*.9f,true);
                    if(body.contact!=null)body.contact.SetStrain(strain);
                    CompetitionShowFamilyFace(id,instrument);
                }
        }

        private void CompetitionStance(CharacterPresentation visual,bool arrived,CompetitionApparatus instrument)
        {
            // A podium, table or course show poses its own field (EpisodeDirector.CompetitionShowFamilies.cs).
            if(competitionShow!=null && competitionShow.Plan.Family!=CompetitionShowFamily.Hold){CompetitionShowFamilyStance(visual,arrived,instrument);return;}
            if(visual==null || !arrived || instrument==null || !visual.IsStill || !visual.CanAct(CharacterPresentation.BodyActivity.Posing))return;
            if(!competitionPoses.TryGetValue(visual,out var own))
            {
                if(visual.Activity!=CharacterPresentation.BodyActivity.None)return;
                own=new CompetitionPose{previous=visual.HeldPose};competitionPoses.Add(visual,own);
            }
            else if(visual.Activity!=CharacterPresentation.BodyActivity.Posing)return;
            bool effort=instrument.Instrument==CompetitionApparatus.Family.GripRig && CompetitionArenaPlaying(instrument.ActorId);
            if(visual.gameObject==player.gameObject && competitionShow==null)effort&=challengeRun!=null && challengeRun.Holding;
            own.written=effort?CharacterPresentation.Pose.PowerStance:CharacterPresentation.Pose.AtEase;
            visual.SetPose(own.written);visual.SetActivity(CharacterPresentation.BodyActivity.Posing);
            var contact=own.contact;
            if(contact==null)
            {
                contact=visual.GetComponent<CompetitionInstrumentPose>();
                if(contact==null)contact=visual.gameObject.AddComponent<CompetitionInstrumentPose>();
                own.contact=contact;
            }
            bool mine=visual.gameObject==player.gameObject;
            string id=mine?CompetitionArenaPlayerId:instrument.ActorId;
            if(id!=null && competitionBodies.TryGetValue(id,out var body))body.contact=contact;
            contact.SetFitGate(competitionFitGate);
            contact.Bind(instrument,mine?challengeRun:null,CompetitionArenaPlaying(instrument.ActorId),CompetitionArenaPaused,arrived);
        }

        private void ReleaseCompetitionStance(CharacterPresentation visual)
        {
            if(visual==null || !competitionPoses.TryGetValue(visual,out var own))return;
            if(own.contact!=null)own.contact.Release();
            if(visual.Activity==own.writtenActivity && visual.HeldPose==own.written)
            {visual.SetActivity(CharacterPresentation.BodyActivity.None);visual.SetPose(own.previous);}
            competitionPoses.Remove(visual);
        }

        private void TickCompetitionArena()
        {
            // A finished ranked result on its plate is past every staging check: a houseguest
            // nudged off their mark in those 0.9 s must not throw the result away.
            if(!competitionArenaStaging || challengeFinishHold>0f)return;
            // A show stations the player only when they competed, and lets them go when they drop:
            // from then on their route is to the side, which the station's checks do not cover.
            bool show=competitionShow!=null;
            bool playerReleased=show && CompetitionShowReleased(competitionShow.Plan.PlayerId);
            bool playerStationed=!show || competitionPlayerStation!=null && !playerReleased;
            if(playerStationed && (player==null || competitionPlayerStation==null || !competitionPlayerStation.isActiveAndEnabled
                || (competitionPlayerStation.Position-competitionPlayerStationPosition).sqrMagnitude>.0025f
                || Mathf.Abs(Mathf.DeltaAngle(competitionPlayerStation.Facing,competitionPlayerStationFacing))>.5f
                || !player.IsActivityMoveValid(competitionPlayerOwner)))
            { CompetitionArenaRefused("Your competition route changed. You are back at the briefing: try again.");return; }
            if(player==null){CompetitionArenaRefused("The player's body is gone.");return;}
            competitionArenaWasReady|=CompetitionArenaReady;
            bool paused=CompetitionArenaPaused;
            player.PauseActivityMove(competitionPlayerOwner,paused);
            bool castStaged=npcMeetings!=null && npcMeetings.HasCompetitionStage;
            if(castStaged && !npcMeetings.ValidateCompetitionStage(out var reason))
            {
                // A show without its field is no show: its card plays.
                if(show){CompetitionArenaRefused(reason);return;}
                EndCompetitionCast();competitionAudienceStatus=reason+" The eligible field is still listed.";castStaged=false;
            }
            if(castStaged)npcMeetings.SetCompetitionStagePaused(paused);
            SyncCompetitionInstruments();
            if(paused){competitionAssemblyDeadline+=Time.unscaledDeltaTime;return;}
            if(playerReleased)TickCompetitionShowPlayerReleased();
            else if(playerStationed)
                player.GetComponent<CharacterPresentation>()?.SetFacing(player.ActivityHasArrived(competitionPlayerOwner)?competitionPlayerStation.Facing:float.NaN);
            string arenaPlayer=CompetitionArenaPlayerId;
            if(arenaPlayer!=null && playerStationed && competitionInstruments.TryGetValue(arenaPlayer,out var playerInstrument))
            {
                CompetitionStance(player.GetComponent<CharacterPresentation>(),player.ActivityHasArrived(competitionPlayerOwner),playerInstrument);
                // Endurance's translucent play area was intended to show the effort. The assembly
                // keeps its wide shot; in play the camera can read the actor's hands and pressure rig.
                if(!show && !competitionEffortFramed && CompetitionArenaReady && competitionScreen!=null && competitionScreen.IsPlaying
                    && playerInstrument.Instrument==CompetitionApparatus.Family.GripRig && cameraRig!=null && !cameraRig.ReducedMotion)
                {
                    competitionEffortFramed=true;
                    // The yard, as every competition shot names it (D7), drawn on its way with what the view drew (RS6a).
                    cameraRig.MoveTo(new HouseCameraRig.Shot
                    {
                        Focus=competitionPlayerStationPosition+Vector3.up*1.05f+competitionPlayerStation.transform.forward*.20f,
                        Distance=6.6f,Pitch=19f,Yaw=competitionPlayerStationFacing+35f,FieldOfView=40f,Seconds=CompetitionShotSeconds,
                        Rooms=CompetitionRooms,TravelFromRooms=ViewedRooms,
                    });
                    competitionFramed=true;
                }
            }
            // At a tower show the player stands at the stacks with no instrument of their own (EpisodeDirector.TowerShow.cs).
            else if(arenaPlayer!=null && playerStationed && CompetitionTowerShowRunning)
                CompetitionTowerShowStance(player.GetComponent<CharacterPresentation>(),player.ActivityHasArrived(competitionPlayerOwner),arenaPlayer);
            foreach(var pair in competitionArenaActors)
            {
                if(!competitionBodies.TryGetValue(pair.Key,out var body) || body.root==null)continue;
                var npc=body.root;
                var visual=body.visual;
                if(visual==null)continue;
                if(CompetitionShowReleased(pair.Key)){TickCompetitionShowReleased(pair.Key,body);continue;}
                bool arrived=CompetitionBodyOccupiesStation(pair.Key,body,false);
                visual.SetTalking(false);visual.SetArguing(false);visual.SetFacing(arrived?CompetitionTowerShowFacing(pair.Key,pair.Value.Position,pair.Value.Facing):float.NaN);
                // One who let go around the player stands easy by their rig; a tower show's field has no instrument.
                if(competitionInstruments.TryGetValue(pair.Key,out var instrument)){if(!CompetitionGameDropped(pair.Key))CompetitionStance(visual,arrived,instrument);}
                else if(CompetitionTowerShowRunning)CompetitionTowerShowStance(visual,arrived,pair.Key);
                if(arrived && pair.Value.Seated)
                {
                    // Explicitly: a missing component is Unity's fake null in the editor, which ?? keeps.
                    var seat=npc.GetComponent<HouseSeatPresentation>();
                    if(seat==null)seat=npc.AddComponent<HouseSeatPresentation>();
                    if(seat.isActiveAndEnabled && !seat.Active)
                    {
                        string id=pair.Key;
                        seat.Begin(pair.Value,()=>competitionArenaStaging && npcMeetings!=null && npcMeetings.CompetitionActorArrived(id));
                        if(seat.Active && !competitionAudienceSeats.Contains(seat))competitionAudienceSeats.Add(seat);
                    }
                }
                else visual.SetSeated(false);
            }
            int arrivals=(playerStationed && player.ActivityHasArrived(competitionPlayerOwner)?1:0)+(castStaged?npcMeetings.CompetitionArrivals:0);
            int total=(playerStationed?1:0)+(castStaged?npcMeetings.CompetitionStageCount:0);
            competitionArenaStatus=arrivals+" / "+total+" houseguests in position. "+competitionAudienceStatus;
            // The deadline bounds the initial assembly, not an attempt whose whole
            // native field already arrived. Pausing a running attempt re-establishes
            // its native stopped proof on resume; that cannot expire its old assembly.
            // A show bounds its own assembly and starts with whoever is there (EpisodeDirector.CompetitionShow.cs).
            if(!show && !competitionArenaWasReady && Time.unscaledTime>competitionAssemblyDeadline && !CompetitionArenaReady)
            {
                Debug.Log("[Gamesim competition stage] native assembly deadline. "+CompetitionStageDiagnostic(),this);
                if(!player.ActivityHasArrived(competitionPlayerOwner))
                {message="Your competition route could not finish. Your attempt has not started.";BeginSystemNotice();CancelChallenge();}
                else {EndCompetitionCast();competitionAudienceStatus="Some audience routes did not finish. The eligible field remains listed.";}
            }
        }

        private bool CompetitionArenaReady => competitionArenaStaging && player!=null
            && (competitionShow!=null && (competitionPlayerStation==null || CompetitionShowReleased(competitionShow.Plan.PlayerId))
                || player.ActivityHasArrived(competitionPlayerOwner))
            && (npcMeetings==null || !npcMeetings.HasCompetitionStage || npcMeetings.CompetitionArrivals==npcMeetings.CompetitionStageCount);

        private void EndCompetitionCast()
        {
            foreach(var seat in competitionAudienceSeats)if(seat!=null)seat.End();
            competitionAudienceSeats.Clear(); npcMeetings?.EndCompetitionStage();
            foreach(var pair in competitionArenaActors)
            {
                if(competitionBodies.TryGetValue(pair.Key,out var body))
                {
                    if(body.contact!=null)body.contact.Release();
                    ReleaseCompetitionStance(body.visual);body.visual?.SetFacing(float.NaN);
                    if(body.root!=null)competitionRouteOwners.Remove(body.root.transform);
                }
                competitionInstruments.Remove(pair.Key);
                competitionBodies.Remove(pair.Key);
                competitionFootprints.Remove(pair.Key);competitionFootprints.Remove(pair.Key+":approach");
                // Authored furniture belongs to the scene. Only transient stage anchors are ours.
                if(pair.Value!=null && competitionArenaRoot!=null && pair.Value.transform.IsChildOf(competitionArenaRoot.transform))
                    Destroy(pair.Value.gameObject);
            }
            competitionArenaActors.Clear();
        }

        private void EndCompetitionArena()
        {
            try
            {
                string arenaPlayer=CompetitionArenaPlayerId;
                competitionArenaStaging=false;EndCompetitionCast();
                if(arenaPlayer!=null && competitionBodies.TryGetValue(arenaPlayer,out var playerBody) && playerBody.contact!=null)
                    playerBody.contact.Release();
                foreach(var visual in competitionPoses.Keys.ToArray())ReleaseCompetitionStance(visual);
                if(player!=null && player.ReleaseActivityMove(competitionPlayerOwner))
                {
                    var playerVisual=player.GetComponent<CharacterPresentation>();
                    if(playerVisual!=null)playerVisual.SetFacing(float.NaN);
                }
                competitionPlayerStation=null;
                competitionEffortFramed=false;
                competitionArenaWasReady=false;
                competitionSign=null;competitionArenaMarks.Clear();competitionOverlaysShown=true;
                competitionInstruments.Clear();
                competitionBodies.Clear();
                competitionFootprints.Clear();
                competitionRouteOwners.Clear();competitionStageFloor=null;competitionScenery=null;competitionFitGate=null;
                if(competitionArenaRoot!=null){Destroy(competitionArenaRoot);competitionArenaRoot=null;}
            }
            finally
            {
                // The yard as it was, whatever ended the arena - a throw in the teardown above included;
                // and a show running on it is over.
                competitionArenaStaging=false;
                if(competitionYard.State==CompetitionYardDressing.YardState.Competition)competitionYard.Restore();
                EndCompetitionShowState();
            }
        }
    }
}
