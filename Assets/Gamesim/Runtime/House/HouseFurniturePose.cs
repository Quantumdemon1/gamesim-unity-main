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
        private bool previousSeated;
        private bool ending;
        private Transform arm,body;
        private Quaternion armBase,lastArm;
        private bool armWritten;
        public bool Active { get; private set; }
        public bool IsPerforming => Active && arrivedAt>=0 && Time.unscaledTime-arrivedAt>=.25f;
        public bool IsGestureApplied => Active && !ending && armWritten && arm!=null && visual!=null && body==visual.VisualRoot;

        public void Begin(HouseInteractionAnchor target,HouseFurnitureActivity activity,float seconds,
            Func<bool> owns,Func<bool> reached,Action completed,Func<bool> paused=null)
        {
            End();visual=GetComponent<CharacterPresentation>();
            if(!isActiveAndEnabled || target==null || visual==null)return;
            anchor=target;kind=activity;duration=Mathf.Clamp(seconds,2,30);valid=owns;arrived=reached;finished=completed;isPaused=paused;
            arrivedAt=-1;until=float.PositiveInfinity;routeDeadline=Time.unscaledTime+20f;
            previousFacing=visual.FacingYaw;previousSeated=visual.IsSeated;ending=false;Active=true;
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
            if(anchor.Seated)
            {
                seat=GetComponent<HouseSeatPresentation>() ?? gameObject.AddComponent<HouseSeatPresentation>();
                seat.Begin(anchor,()=>Active && valid!=null && valid());
                if(!seat.Active){FinishNow();return;}
            }
            else if(kind==HouseFurnitureActivity.PrepareSnack)CounterGesture();
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
            if(seat!=null && seat.Active)seat.RequestExit();else FinishNow();
        }

        public void End()
        {
            if(!Active)return;
            Active=false;ending=false;RestoreArm();seat?.End();
            if(visual!=null){visual.SetSeated(previousSeated);visual.SetFacing(previousFacing);}
            valid=null;arrived=null;isPaused=null;finished=null;anchor=null;body=arm=null;
        }

        private void RestoreArm()
        {
            if(arm!=null && armWritten && Quaternion.Angle(arm.localRotation,lastArm)<.01f)arm.localRotation=armBase;
            armWritten=false;
        }
        private void FinishNow(){var done=finished;End();done?.Invoke();}
        // Owner reconciliation/disposal retires the lease. Never rebuild UI during unload.
        private void OnDisable()=>End();
        private void OnDestroy()=>End();
    }
}
