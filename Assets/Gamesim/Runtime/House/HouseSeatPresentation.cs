using System;
using System.Collections.Generic;
using Gamesim.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.House
{
    /// <summary>
    /// The one owner of a body posed at furniture: sitting in a seat, lying on a bed, or in the
    /// pool. The navigation and collider root waits at the approach; only the visual body moves.
    /// </summary>
    [DisallowMultipleComponent,DefaultExecutionOrder(190)]
    public sealed class HouseSeatPresentation : MonoBehaviour
    {
        private CharacterPresentation character;
        private HouseInteractionAnchor anchor;
        private Func<bool> ownsSeat;
        private Transform body,hips,leftFoot,rightFoot,head;
        private static readonly HashSet<HouseSeatPresentation> occupied = new HashSet<HouseSeatPresentation>();
        private static readonly RaycastHit[] pickHits = new RaycastHit[32];
        private Vector3 origin,anchorPosition,anchorContact,appliedOffset,lastWritten,baseLocalPosition;
        private Quaternion baseLocalRotation,startRotation;
        /// <summary>The way the posed body last faced, for a body that takes its place after the old one has gone.</summary>
        private Quaternion heldRotation=Quaternion.identity;
        private float began,nextScan,anchorFacing;
        private HouseAnchorPose mode;
        /// <summary>Where a swimmer is along the pool, in metres from its middle, and which way they are going.</summary>
        private float lap, lapHeading = 1f, lapTurnedAt = float.NegativeInfinity, lapRestUntil;

        /// <summary>How a swimmer uses the pool: strokes along it at this pace, turns at the ends, and treads water for a while between lengths.</summary>
        private const float LapPace = .75f, LapTurnSeconds = .9f, LapRest = 3.5f, LapMargin = 1f;

        /// <summary>
        /// How long a body takes to step from its root onto a seat, standing, before it sits. The
        /// sit was cued on the first frame while the body took three quarters of a second to reach
        /// the seat, so every sit-down played on the open floor at the approach - half a metre
        /// short of a couch, in the middle of the living room's U - and slid onto the cushion
        /// after (PACK8-PASS-PLAN A2). Now the body is on the seat when it sits.
        /// </summary>
        public const float StepSeconds = .38f;

        /// <summary>
        /// From the sit being cued: when the hips are fitted to the cushion (the controller's
        /// cross-fade into the seat is 0.35 s), how long the fit eases in, and when the body is settled.
        /// </summary>
        private const float FitDelay = .45f, FitSeconds = .3f, SettleSeconds = .85f;

        /// <summary>How the body is placed: seated, lying or in the water.</summary>
        public HouseAnchorPose Mode => Active ? mode : HouseAnchorPose.Stand;
        private bool exiting;
        private float exitBegan;
        private Vector3 exitOffset;
        public bool IsExiting => Active && exiting;
        public bool Active { get; private set; }
        public bool Settled => Active && !exiting && body!=null && (character.ReducedMotion || Time.unscaledTime-PoseBegan>=SettleSeconds);

        /// <summary>
        /// Whether the body is still stepping onto its seat, on its feet. Only a seat has a step: a
        /// bed and the pool lay the body down as it arrives, as they always did. Reduced motion
        /// sits at once.
        /// </summary>
        public bool IsStepping => Active && !exiting && mode==HouseAnchorPose.Seat && character!=null && !character.ReducedMotion
            && Time.unscaledTime-began<StepSeconds;

        /// <summary>When the pose itself is cued: after the step for a seat, at once for anything else.</summary>
        private float PoseBegan => began+(mode==HouseAnchorPose.Seat && character!=null && !character.ReducedMotion ? StepSeconds : 0f);
        public Vector3 VisualFocus => head!=null ? head.position : (body!=null ? body.position : transform.position)+Vector3.up*1.35f;
        public Vector3 VisualFeet => body!=null ? new Vector3(body.position.x,transform.position.y,body.position.z) : transform.position;

        /// <summary>Only a current, settled, owned body can supply a seated observation endpoint.</summary>
        public bool TryGetWitnessPoint(out Vector3 point)
        {
            point=default;
            if(!isActiveAndEnabled || !Settled || character==null || !character.isActiveAndEnabled
                || body==null || !body.gameObject.activeInHierarchy || character.VisualRoot!=body
                || anchor==null || !anchor.isActiveAndEnabled || anchor.gameObject.scene!=gameObject.scene
                || (anchor.Position-anchorPosition).sqrMagnitude>.0025f || (anchor.SeatContact-anchorContact).sqrMagnitude>.0025f
                || Mathf.Abs(Mathf.DeltaAngle(anchor.Facing,anchorFacing))>1f
                || (transform.position-origin).sqrMagnitude>.16f || ownsSeat==null || !ownsSeat())return false;
            var candidate=VisualFocus;
            if(!HouseRoomQuery.Finite(candidate) || (candidate-transform.position).sqrMagnitude>3.5f*3.5f)return false;
            point=candidate;return true;
        }

        public void Begin(HouseInteractionAnchor target,Func<bool> valid)
        {
            if(!isActiveAndEnabled)return;
            if(Active && anchor==target && !exiting)return;
            End();
            character=GetComponent<CharacterPresentation>();
            if(target==null || character==null || !target.Posed)return;
            anchor=target;anchorPosition=target.Position;anchorContact=target.SeatContact;anchorFacing=target.Facing;origin=transform.position;ownsSeat=valid;
            began=Time.unscaledTime;nextScan=0;Active=true;mode=target.Pose;
            lap=0f;lapHeading=1f;lapTurnedAt=float.NegativeInfinity;lapRestUntil=Time.unscaledTime+LapRest;floatDepth=-1f;
            occupied.Add(this);
            Park(true);
            Cue();
        }

        /// <summary>
        /// The root's agent parked while the body sits and a body again when it rises: a seated
        /// body's root stays on its approach, and at its full radius it blocked the approaches
        /// beside it, so a sofa's third seat never filled. The player has no motion owner here;
        /// the stage that seats them parks their agent itself.
        /// </summary>
        private void Park(bool value)
        {
            var motion=GetComponent<HouseNpcMotion>();
            if(motion!=null)motion.SetParked(value);
        }

        /// <summary>
        /// Says what the body is doing, every frame it holds the pose. A body that cannot lie down
        /// or swim - a rig with no state for it - sits instead, which is at least on the furniture.
        /// </summary>
        private void Cue()
        {
            if(mode==HouseAnchorPose.Lie && character.CanAct(CharacterPresentation.BodyActivity.Sleeping))
            {character.SetSeated(false);character.SetActivity(CharacterPresentation.BodyActivity.Sleeping);return;}
            if(mode==HouseAnchorPose.Float && character.CanAct(CharacterPresentation.BodyActivity.Swimming))
            {character.SetSeated(false);character.SetActivity(CharacterPresentation.BodyActivity.Swimming,Stroking);return;}
            character.SetActivity(CharacterPresentation.BodyActivity.None);
            // On its feet until it is on the seat: the sit is played where it is sat.
            character.SetSeated(!IsStepping);
        }

        /// <summary>Whether a swimmer is doing a length right now, rather than treading water or turning.</summary>
        private bool Stroking => mode==HouseAnchorPose.Float && Time.unscaledTime>=lapRestUntil
            && Time.unscaledTime-lapTurnedAt>=LapTurnSeconds && !character.ReducedMotion
            && character.CanAct(CharacterPresentation.BodyActivity.Swimming);

        /// <summary>How far under the water line the hips are: shallow for a stroke, deep for treading water, eased between.</summary>
        private float floatDepth=-1f;
        private const float StrokeDepth=-.12f, TreadDepth=-.3f, DepthPerSecond=.4f;

        /// <summary>
        /// Moves a swimmer along the pool: a length at a slow crawl, a turn at the end, a rest
        /// treading water, and back. Only the visual body swims; the root waits at the side.
        /// </summary>
        private void TickLap()
        {
            if(mode!=HouseAnchorPose.Float || character.ReducedMotion || !character.CanAct(CharacterPresentation.BodyActivity.Swimming))return;
            float reach=Mathf.Max(0f,HouseActivityAnchors.PoolBasinLength*.5f-LapMargin);
            if(!Stroking)return;
            lap+=lapHeading*LapPace*Time.unscaledDeltaTime;
            if(Mathf.Abs(lap)>=reach)
            {
                lap=Mathf.Clamp(lap,-reach,reach);lapHeading=-lapHeading;lapTurnedAt=Time.unscaledTime;
                // Every other end, a breather.
                if(lapHeading>0f)lapRestUntil=Time.unscaledTime+LapTurnSeconds+LapRest;
            }
        }

        private void LateUpdate()
        {
            if(!Active)return;
            // Nothing left to get up from, or to get up with: the seat gone, moved or turned, the
            // body gone, or the root pushed off its approach. The body goes back to its root now.
            if(character==null || !character.isActiveAndEnabled || anchor==null || !anchor.isActiveAndEnabled
                || (anchor.Position-anchorPosition).sqrMagnitude>.0025f || (anchor.SeatContact-anchorContact).sqrMagnitude>.0025f
                || Mathf.Abs(Mathf.DeltaAngle(anchor.Facing,anchorFacing))>1f
                || (transform.position-origin).sqrMagnitude>.16f)
            {End();return;}
            // Getting up goes on after whoever held the seat has let go of it. Letting go is what
            // asks a body to stand, and the owner was asked first: a chat that ended, an activity
            // taken away and every ceremony's release all cut the stand-up off on its first frame,
            // and the body snapped from the chair to the approach still sitting (PACK8-PASS-PLAN A2).
            if(exiting){TickExit();return;}
            if(ownsSeat==null || !ownsSeat()){End();return;}
            Cue();
            TickLap();
            if(character.VisualRoot!=body)
            {
                RestoreBody();body=character.VisualRoot;hips=leftFoot=rightFoot=head=null;nextScan=0;
                if(body==null)return;
                baseLocalPosition=body.localPosition;baseLocalRotation=body.localRotation;
                lastWritten=baseLocalPosition;startRotation=body.rotation;
            }
            if(body==null)return;
            // Primitive poses rewrite their own base offset every frame. Humanoids do not; remove
            // only our previous contribution, never accumulate it or overwrite an arriving body.
            if((body.localPosition-lastWritten).sqrMagnitude<.000001f)body.localPosition-=appliedOffset;
            baseLocalPosition=body.localPosition;
            body.localRotation=baseLocalRotation;
            if(hips==null && Time.unscaledTime>=nextScan)
            {
                nextScan=Time.unscaledTime+.25f;
                var animator=body.GetComponentInChildren<Animator>();
                if(animator!=null && animator.isHuman && animator.avatar!=null)
                {
                    hips=animator.GetBoneTransform(HumanBodyBones.Hips);
                    leftFoot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                    rightFoot=animator.GetBoneTransform(HumanBodyBones.RightFoot);
                    head=animator.GetBoneTransform(HumanBodyBones.Head);
                }
                if(head==null)
                    foreach(var bone in body.GetComponentsInChildren<Transform>())
                        if(bone.name=="Head" || bone.name=="Head pivot"){head=bone;break;}
            }
            if(mode!=HouseAnchorPose.Seat){PoseAlong(character.ReducedMotion ? 1f : Mathf.SmoothStep(0,1,(Time.unscaledTime-began)/.75f));return;}
            // The step: from the root onto the seat and round into it, standing. The sit is cued
            // when it ends (Cue), and the hips are fitted once the sit has played in.
            float blend=character.ReducedMotion ? 1f : Mathf.SmoothStep(0,1,(Time.unscaledTime-began)/StepSeconds);
            // Every body sits the way the anchor says. A half-turn used to be added for bodies with no
            // humanoid avatar, whose Blender-authored seated clips were exported facing the other
            // way; those bodies are gone (the cast is UMA's alone, 2026-09-27), and the half-turn was
            // left seating the primitive rig facing the chair back, and turning a UMA body round
            // on the frame its avatar arrived. The importer turns every humanoid take to face its
            // body's forward (AuthoredAssetImporter.OnPreprocessAnimation).
            //
            // Through baseLocalRotation rather than over it, as well: this line used to set the
            // WORLD rotation and discard the base for the whole time somebody was sitting - the
            // value captured two methods up and carefully restored on the way out.
            body.rotation=Quaternion.Slerp(startRotation,
                Quaternion.Euler(0,anchor.Facing,0)*baseLocalRotation,blend);
            Vector3 offset=anchor.Position-transform.position;
            float sat=Time.unscaledTime-PoseBegan;
            if(hips!=null && sat>FitDelay)
            {
                float leg=leftFoot!=null ? Vector3.Distance(hips.position,leftFoot.position) : .7f;
                // The hip joint is above the cushion contact. Scale that clearance to this body.
                var contact=anchor.SeatContact+Vector3.up*Mathf.Clamp(leg*.14f,.06f,.16f);
                var fitted=contact-hips.position;
                float fit=character.ReducedMotion ? 1f : Mathf.Clamp01((sat-FitDelay)/FitSeconds);
                offset=Vector3.Lerp(offset,fitted,fit);
                if(leftFoot!=null && rightFoot!=null)
                {
                    float soles=Mathf.Min(leftFoot.position.y,rightFoot.position.y)+offset.y;
                    offset.y+=Mathf.Max(0,anchor.Position.y+.035f-soles);
                }
            }
            var local=body.parent.InverseTransformVector(offset*blend);
            body.localPosition=baseLocalPosition+local;appliedOffset=local;lastWritten=body.localPosition;
            heldRotation=body.rotation;
        }

        /// <summary>
        /// Lying and floating: the body turned to run along the anchor, head first - the takes are
        /// trimmed to lie head-forward - with the hips on the mattress or at the water line.
        /// </summary>
        private void PoseAlong(float blend)
        {
            // A swimmer turns at the end of a length over the turn's own time, not in a frame.
            float heading=anchor.Facing;
            if(mode==HouseAnchorPose.Float)
            {
                float turn=Mathf.Clamp01((Time.unscaledTime-lapTurnedAt)/LapTurnSeconds);
                float from=lapHeading>0f ? 180f : 0f, to=lapHeading>0f ? 0f : 180f;
                heading+=Mathf.LerpAngle(from,to,Mathf.SmoothStep(0,1,turn));
            }
            body.rotation=Quaternion.Slerp(startRotation,Quaternion.Euler(0,heading,0)*baseLocalRotation,blend);
            var along=Quaternion.Euler(0,anchor.Facing,0)*Vector3.forward;
            // The hips' place: on the mattress, clear of it by the body's thickness; in the water,
            // at the line when swimming and chest-deep when treading water.
            // A body that rolls from treading water into a stroke rises to the line over a moment,
            // not in the frame the stroke begins.
            if(mode==HouseAnchorPose.Float)
            {
                float wanted=Stroking ? StrokeDepth : TreadDepth;
                floatDepth=floatDepth<-.9f || character.ReducedMotion ? wanted : Mathf.MoveTowards(floatDepth,wanted,DepthPerSecond*Time.unscaledDeltaTime);
            }
            var contact=anchor.SeatContact+along*(mode==HouseAnchorPose.Float ? lap : 0f)
                + Vector3.up*(mode==HouseAnchorPose.Lie ? .1f : floatDepth);
            Vector3 offset=anchor.Position-transform.position;
            if(hips!=null)
            {
                float fit=character.ReducedMotion ? 1f : Mathf.Clamp01((Time.unscaledTime-began-.3f)/.45f);
                offset=Vector3.Lerp(offset,contact-hips.position,fit);
            }
            var local=body.parent.InverseTransformVector(offset*blend);
            body.localPosition=baseLocalPosition+local;appliedOffset=local;lastWritten=body.localPosition;
            heldRotation=body.rotation;
        }

        private void RestoreBody()
        {
            if(body!=null)
            {
                if((body.localPosition-lastWritten).sqrMagnitude<.000001f)body.localPosition-=appliedOffset;
                body.localRotation=baseLocalRotation;
            }
            appliedOffset=Vector3.zero;
        }

        public void End()
        {
            occupied.Remove(this);
            if(!Active)return;
            Active=false;exiting=false;RestoreBody();
            Park(false);
            // Stood up, not put back as found: only a pose that holds a body in a seat may say it
            // sits, and a flag found set when this pose began - one set with no seat under it -
            // was handed back when it ended, and sat the body down in the air at its approach.
            if(character!=null){character.SetActivity(CharacterPresentation.BodyActivity.None);character.SetSeated(false);}
            body=hips=leftFoot=rightFoot=head=null;anchor=null;ownsSeat=null;
        }

        /// <summary>Normal departure stands at the chair before returning the visual body to its clear approach.</summary>
        public void RequestExit()
        {
            if(!Active || exiting)return;
            if(character==null || character.ReducedMotion){End();return;}
            exiting=true;exitBegan=Time.unscaledTime;exitOffset=appliedOffset;
            character.SetActivity(CharacterPresentation.BodyActivity.None);
            character.SetSeated(false);
        }

        private void TickExit()
        {
            if(character.VisualRoot!=body)
            {
                // A change of clothes finishing during the climb out - the change back out of the
                // swimwear starts as they get up - hands over to the new body, which faces the way
                // the old one faced and climbs on from where it had got to. Ending here instead
                // dropped the climb halfway and jumped the body to the deck.
                var facing=body!=null ? body.rotation : heldRotation;
                RestoreBody();
                body=character.VisualRoot;hips=leftFoot=rightFoot=head=null;
                if(body==null){End();return;}
                baseLocalPosition=body.localPosition;baseLocalRotation=body.localRotation;lastWritten=baseLocalPosition;
                body.rotation=facing;
            }
            character.SetActivity(CharacterPresentation.BodyActivity.None);
            character.SetSeated(false);
            float elapsed=Time.unscaledTime-exitBegan;
            // The controller's stand cross-fade finishes before the short, clear seat approach.
            float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-.35f)/.35f));
            if(body!=null)
            {
                if((body.localPosition-lastWritten).sqrMagnitude<.000001f)body.localPosition-=appliedOffset;
                baseLocalPosition=body.localPosition;
                appliedOffset=exitOffset*(1-blend);
                body.localPosition=baseLocalPosition+appliedOffset;lastWritten=body.localPosition;
                heldRotation=body.rotation;
            }
            if(elapsed>=.7f)End();
        }

        /// <summary>Presentation picking uses visible body bounds and real occlusion, never extra physics colliders.</summary>
        public static bool TryPickNpc(Scene scene,Ray ray,out HouseNpc npc)
        {
            npc=null;float nearest=500f;
            foreach(var seat in occupied)
            {
                if(seat==null || !seat.Active || seat.body==null || seat.gameObject.scene!=scene)continue;
                var candidate=seat.GetComponent<HouseNpc>();
                if(candidate==null || !candidate.isActiveAndEnabled)continue;
                float distance=nearest;
                bool hitBody=false;
                foreach(var renderer in seat.body.GetComponentsInChildren<Renderer>())
                    if(renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.bounds.IntersectRay(ray,out float hit)
                        && hit>=0 && hit<distance){distance=hit;hitBody=true;}
                if(!hitBody)continue;
                // Sight, not Pick: this is the occlusion half of the pick, asking whether anything
                // stands between the camera and the body. Furniture standing in front of a seated
                // houseguest is exactly what you are looking THROUGH to click them.
                int count=Physics.RaycastNonAlloc(ray,pickHits,distance,HouseLayers.Sight,QueryTriggerInteraction.Ignore);
                if(count==pickHits.Length)continue;
                bool blocked=false;
                for(int index=0;index<count;index++)
                    if(!pickHits[index].transform.IsChildOf(candidate.transform)){blocked=true;break;}
                if(blocked)continue;
                npc=candidate;nearest=distance;
            }
            return npc!=null;
        }
        private void OnDisable()=>End();
        private void OnDestroy()=>End();
    }
}
