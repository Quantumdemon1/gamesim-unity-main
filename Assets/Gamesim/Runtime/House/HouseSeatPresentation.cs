using System;
using System.Collections.Generic;
using Gamesim.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.House
{
    /// <summary>Furniture presentation leaves the reserved navigation/collider root at its approach.</summary>
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

        /// <summary>The half-turn the authored seated clips need to face the way the anchor says.</summary>
        private const float SeatedClipHalfTurn = 180f;
        private float began,nextScan,anchorFacing;
        private bool wasSeated;
        private bool exiting;
        private float exitBegan;
        private Vector3 exitOffset;
        public bool IsExiting => Active && exiting;
        public bool Active { get; private set; }
        public bool Settled => Active && !exiting && body!=null && (character.ReducedMotion || Time.unscaledTime-began>=.85f);
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
            if(target==null || character==null || !target.Seated)return;
            anchor=target;anchorPosition=target.Position;anchorContact=target.SeatContact;anchorFacing=target.Facing;origin=transform.position;ownsSeat=valid;
            wasSeated=character.IsSeated;began=Time.unscaledTime;nextScan=0;Active=true;
            occupied.Add(this);
            character.SetSeated(true);
        }

        private void LateUpdate()
        {
            if(!Active)return;
            if(character==null || !character.isActiveAndEnabled || anchor==null || !anchor.isActiveAndEnabled
                || (anchor.Position-anchorPosition).sqrMagnitude>.0025f || (anchor.SeatContact-anchorContact).sqrMagnitude>.0025f
                || Mathf.Abs(Mathf.DeltaAngle(anchor.Facing,anchorFacing))>1f
                || (transform.position-origin).sqrMagnitude>.16f || ownsSeat==null || !ownsSeat())
            {End();return;}
            if(exiting){TickExit();return;}
            character.SetSeated(true);
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
            float blend=character.ReducedMotion ? 1f : Mathf.SmoothStep(0,1,(Time.unscaledTime-began)/.75f);
            // Half a turn, because the seated clips are authored facing the other way.
            //
            // Everything that could be measured about this shot was already correct - the root faced
            // the camera to within 0.1 degrees, the body sat 0.05 m from the seat on the cushion,
            // the animator was genuinely in a 4.30 s SitIdle - and the diary confessional still
            // framed the back of the player's head for the whole visit. Five explanations were
            // checked and killed before the frame was simply rendered and looked at: the chair mesh
            // (correct, by its own export docstring), the anchor convention (the same rule every
            // other seat uses), the camera side (in front), the Seated parameter (declared), the
            // sit clips (present, playing). What is left is inside the clip, where no transform can
            // see it, so it is corrected where seated facing is applied.
            //
            // Through baseLocalRotation rather than over it, as well: this line used to set the
            // WORLD rotation and discard the base for the whole time somebody was sitting - the
            // value captured two methods up and carefully restored on the way out.
            body.rotation=Quaternion.Slerp(startRotation,
                Quaternion.Euler(0,anchor.Facing+SeatedClipHalfTurn,0)*baseLocalRotation,blend);
            Vector3 offset=anchor.Position-transform.position;
            if(hips!=null && Time.unscaledTime-began>.45f)
            {
                float leg=leftFoot!=null ? Vector3.Distance(hips.position,leftFoot.position) : .7f;
                // The hip joint is above the cushion contact. Scale that clearance to this body.
                var contact=anchor.SeatContact+Vector3.up*Mathf.Clamp(leg*.14f,.06f,.16f);
                var fitted=contact-hips.position;
                float fit=character.ReducedMotion ? 1f : Mathf.Clamp01((Time.unscaledTime-began-.45f)/.3f);
                offset=Vector3.Lerp(offset,fitted,fit);
                if(leftFoot!=null && rightFoot!=null)
                {
                    float soles=Mathf.Min(leftFoot.position.y,rightFoot.position.y)+offset.y;
                    offset.y+=Mathf.Max(0,anchor.Position.y+.035f-soles);
                }
            }
            var local=body.parent.InverseTransformVector(offset*blend);
            body.localPosition=baseLocalPosition+local;appliedOffset=local;lastWritten=body.localPosition;
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
            if(character!=null)character.SetSeated(wasSeated);
            body=hips=leftFoot=rightFoot=head=null;anchor=null;ownsSeat=null;
        }

        /// <summary>Normal departure stands at the chair before returning the visual body to its clear approach.</summary>
        public void RequestExit()
        {
            if(!Active || exiting)return;
            if(character==null || character.ReducedMotion){End();return;}
            exiting=true;exitBegan=Time.unscaledTime;exitOffset=appliedOffset;
            character.SetSeated(false);
        }

        private void TickExit()
        {
            if(character.VisualRoot!=body){End();return;}
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
