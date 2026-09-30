using System;
using Gamesim.Presentation;
using UnityEngine;

namespace Gamesim.House
{
    /// <summary>Short visual activity. Movement, capacity and interruption stay with the reservation owner.</summary>
    [DisallowMultipleComponent,DefaultExecutionOrder(205)]
    public sealed class HouseFurniturePose : MonoBehaviour
    {
        private CharacterPresentation visual;
        private HouseSeatPresentation seat;
        private HouseInteractionAnchor anchor;
        private HouseFurnitureActivity kind;
        private Func<bool> valid,arrived,isPaused;
        private Action finished;
        private float arrivedAt,until,duration,routeDeadline,nextArmScan;
        private float previousFacing;
        private bool ending;
        private Transform arm,body;
        private Quaternion armBase,lastArm;
        private bool armWritten;
        public bool Active { get; private set; }
        public bool IsPerforming => Active && arrivedAt>=0 && Time.unscaledTime-arrivedAt>=.25f;
        public bool IsGestureApplied => Active && !ending && armWritten && arm!=null && visual!=null && body==visual.VisualRoot;
        /// <summary>What is being done here: the activity this pose was begun for.</summary>
        public HouseFurnitureActivity Kind => kind;
        /// <summary>Whether the body is on its way out of the pose: getting up, climbing out.</summary>
        public bool Ending => Active && ending;
        /// <summary>The furniture this pose is at, while it holds.</summary>
        public HouseInteractionAnchor Anchor => Active ? anchor : null;

        public void Begin(HouseInteractionAnchor target,HouseFurnitureActivity activity,float seconds,
            Func<bool> owns,Func<bool> reached,Action completed,Func<bool> paused=null)
        {
            End();visual=GetComponent<CharacterPresentation>();
            if(!isActiveAndEnabled || target==null || visual==null)return;
            // Some things last until the player moves - nobody is woken on a timer.
            duration=float.IsPositiveInfinity(seconds) ? seconds : Mathf.Clamp(seconds,2,30);
            anchor=target;kind=activity;valid=owns;arrived=reached;finished=completed;isPaused=paused;
            // Long enough to walk there, however far "there" is: a trip across the house to the pool
            // at a walk used to time out on the twenty seconds a counter across the kitchen needed.
            float route=Vector3.Distance(transform.position,target.Approach);
            arrivedAt=-1;until=float.PositiveInfinity;routeDeadline=Time.unscaledTime+20f+route/1.5f;
            previousFacing=visual.FacingYaw;ending=false;Active=true;
        }

        private void LateUpdate()
        {
            if(!Active)return;
            if(anchor==null || !anchor.isActiveAndEnabled || visual==null || !visual.isActiveAndEnabled || valid==null || !valid())
            {FinishNow();return;}
            if(isPaused!=null && isPaused())
            {
                // Deadlines use unscaled wall time, so a stalled paused frame must shift them
                // by its full duration. Capping this delta would spend the excess while paused.
                float elapsed=Mathf.Max(0,Time.unscaledDeltaTime);routeDeadline+=elapsed;
                if(arrivedAt>=0){arrivedAt+=elapsed;until+=elapsed;}
                return;
            }
            if(ending)
            {if(seat==null || !seat.Active)FinishNow();return;}
            if(arrived==null || !arrived())
            {if(arrivedAt<0 && Time.unscaledTime>routeDeadline)FinishNow();return;}
            if(arrivedAt<0){arrivedAt=Time.unscaledTime;until=arrivedAt+duration;}
            visual.SetFacing(anchor.Facing);visual.SetTalking(false);visual.SetArguing(false);
            if(!visual.ReducedMotion && Time.unscaledTime-arrivedAt<.25f)return;
            if(anchor.Posed)
            {
                // Explicitly: a missing component is Unity's fake null in the editor, which ?? keeps.
                seat=GetComponent<HouseSeatPresentation>();
                if(seat==null)seat=gameObject.AddComponent<HouseSeatPresentation>();
                seat.Begin(anchor,()=>Active && valid!=null && valid());
                if(!seat.Active){FinishNow();return;}
            }
            else if(kind==HouseFurnitureActivity.Cook && visual.CanAct(CharacterPresentation.BodyActivity.Cooking))
                visual.SetActivity(CharacterPresentation.BodyActivity.Cooking);
            else if(kind==HouseFurnitureActivity.Dance && visual.CanAct(CharacterPresentation.BodyActivity.Dancing))
                visual.SetActivity(CharacterPresentation.BodyActivity.Dancing);
            // A body with no cooking state works the counter with its forearm instead.
            else if(kind==HouseFurnitureActivity.PrepareSnack || kind==HouseFurnitureActivity.Cook)CounterGesture();
            if(Time.unscaledTime>=until)RequestFinish();
        }

        private void CounterGesture()
        {
            if(visual.VisualRoot!=body)
            {
                RestoreArm();body=visual.VisualRoot;arm=null;nextArmScan=0;
            }
            if(arm==null && body!=null && Time.unscaledTime>=nextArmScan)
            {
                // A modular body can expose its root before its humanoid avatar/bones finish.
                nextArmScan=Time.unscaledTime+.25f;
                var animator=body!=null?body.GetComponentInChildren<Animator>():null;
                if(animator!=null && animator.isHuman && animator.avatar!=null)arm=animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            }
            if(arm==null || visual.ReducedMotion)return;
            if(armWritten && Quaternion.Angle(arm.localRotation,lastArm)<.01f)arm.localRotation=armBase;
            armBase=arm.localRotation;
            float reach=12f+8f*Mathf.Sin((Time.unscaledTime-arrivedAt)*2.3f);
            lastArm=armBase*Quaternion.Euler(0,0,-reach);arm.localRotation=lastArm;armWritten=true;
        }

        public void RequestFinish()
        {
            if(!Active || ending)return;
            ending=true;RestoreArm();
            if(visual!=null && !anchor.Posed)visual.SetActivity(CharacterPresentation.BodyActivity.None);
            if(seat!=null && seat.Active)seat.RequestExit();else FinishNow();
        }

        public void End()=>End(false);

        private void End(bool teardown)
        {
            if(!Active)return;
            // A body taken from its seat - the activity released for a conversation, a stage or a
            // reload - gets up at the seat before it goes back to its root, as one whose activity
            // ran out does. The seat ends itself at once when there is nothing to get up from.
            // A pose that is itself taken away, disabled or destroyed, ends its seat at once, as it
            // always did: its owner finds the presentation gone on the next frame and hands the body
            // back then, and a stand-up left running held the body at the chair for another 0.7 s
            // with nothing left to answer for it.
            Active=false;ending=false;RestoreArm();
            if(seat!=null){if(teardown)seat.End();else seat.RequestExit();}
            // Standing: a seat pose is the only thing that may seat a body (HouseSeatPresentation.End).
            if(visual!=null){visual.SetActivity(CharacterPresentation.BodyActivity.None);visual.SetSeated(false);visual.SetFacing(previousFacing);}
            valid=null;arrived=null;isPaused=null;finished=null;anchor=null;body=arm=null;
        }

        private void RestoreArm()
        {
            if(arm!=null && armWritten && Quaternion.Angle(arm.localRotation,lastArm)<.01f)arm.localRotation=armBase;
            armWritten=false;
        }
        private void FinishNow(){var done=finished;End();done?.Invoke();}
        // Owner reconciliation/disposal retires the lease. Never rebuild UI during unload.
        private void OnDisable()=>End(true);
        private void OnDestroy()=>End(true);
    }
}
