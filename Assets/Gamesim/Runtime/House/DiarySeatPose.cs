using System;
using Gamesim.Presentation;
using UnityEngine;
using UnityEngine.AI;

namespace Gamesim.House
{
    /// <summary>Temporary seated presentation after a player has reached the diary approach.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(200)]
    public sealed class DiarySeatPose : MonoBehaviour
    {
        private HouseInteractionAnchor anchor;
        private HouseCameraRig rig;
        private NavMeshAgent agent;
        private CharacterPresentation visual;
        private Collider body;
        private Func<bool> stillOpen;
        private Action interrupted;
        private Vector3 returnPosition, seatPosition;
        private Quaternion returnRotation;
        private bool updatePosition, updateRotation, colliderEnabled, previouslySeated;
        private float previousFacing, nextHeadScan;
        private Transform head;
        private HouseSeatPresentation seatPose;
        private GameObject studio;
        private Material backdropMaterial;
        public enum VisitPhase { None, Approaching, Aligning, Seating, Seated, Leaving }
        private HousePlayerController player;
        private Vector3 approach;
        private float phaseBegan, approachDeadline;
        private Action seated;
        public VisitPhase Phase { get; private set; }
        public bool IsSettled => Active && Phase==VisitPhase.Seated && seatPose!=null && seatPose.Settled;
        public bool Active { get; private set; }
        public Vector3 FacePosition => head!=null ? head.position : transform.position + Vector3.up*1.22f;
        public bool IsOccupying => Active && anchor!=null && anchor.isActiveAndEnabled && seatPose!=null && seatPose.Active
            && Vector3.Distance(transform.position,returnPosition)<.4f;

        public bool Begin(HouseInteractionAnchor seat,HouseCameraRig camera,Func<bool> isOpen,Action onInterrupted=null,Action onSeated=null)
        {
            End();
            agent=GetComponent<NavMeshAgent>(); visual=GetComponent<CharacterPresentation>(); body=GetComponent<Collider>();
            if (!isActiveAndEnabled || seat==null || !seat.Seated || camera==null || visual==null || agent==null || !agent.enabled || !agent.isOnNavMesh
                || seat.gameObject.scene!=gameObject.scene || !seat.isActiveAndEnabled) return false;
            player=GetComponent<HousePlayerController>();
            if(player==null || !player.TryBeginActivityMove(this,seat.Approach,out _))return false;
            anchor=seat; rig=camera; stillOpen=isOpen; interrupted=onInterrupted;
            seated=onSeated;approach=seat.Approach;approachDeadline=Time.unscaledTime+20f;
            returnPosition=transform.position; returnRotation=transform.rotation; seatPosition=seat.Position;
            updatePosition=agent.updatePosition; updateRotation=agent.updateRotation;
            colliderEnabled=body!=null && body.enabled; previouslySeated=visual.IsSeated; previousFacing=visual.FacingYaw;
            // Keep the proven navigation location and path while the actor occupies furniture.
            // The visual returns there before controls resume; the navigation root never teleports.
            seatPose=GetComponent<HouseSeatPresentation>() ?? gameObject.AddComponent<HouseSeatPresentation>();
            head=null; nextHeadScan=0; Active=true;Phase=VisitPhase.Approaching;
            return true;
        }

        private void LateUpdate()
        {
            if (!Active) return;
            if (visual==null || !visual.isActiveAndEnabled || seatPose==null || !seatPose.isActiveAndEnabled
                || anchor==null || !anchor.isActiveAndEnabled || Vector3.Distance(anchor.Position,seatPosition)>.05f
                || Vector3.Distance(anchor.Approach,approach)>.05f || player==null || !player.IsActivityMoveValid(this))
            { Interrupt(); return; }
            if(Phase==VisitPhase.Leaving)
            {
                if(seatPose==null || !seatPose.Active)End();
                return;
            }
            if(stillOpen==null || !stillOpen()){RequestExit();return;}
            if(Phase==VisitPhase.Approaching)
            {
                if(Time.unscaledTime>approachDeadline){Interrupt();return;}
                if(!player.ActivityHasArrived(this))return;
                player.PauseActivityMove(this,true);returnPosition=transform.position;returnRotation=transform.rotation;
                visual.SetFacing(anchor.Facing);Phase=VisitPhase.Aligning;phaseBegan=Time.unscaledTime;return;
            }
            if(Phase==VisitPhase.Aligning)
            {
                if(!visual.ReducedMotion && Time.unscaledTime-phaseBegan<.25f)return;
                agent.updatePosition=false;agent.updateRotation=false;
                if(body!=null)body.enabled=false;
                seatPose.Begin(anchor,()=>Active);
                BuildStudio();Phase=VisitPhase.Seating;phaseBegan=Time.unscaledTime;
            }
            // An external relocation must end access, not get pulled back into the chair.
            if (!IsOccupying) { Interrupt(); return; }
            if(Phase==VisitPhase.Seating && Time.unscaledTime-phaseBegan>15f){Interrupt();return;}
            if(Phase==VisitPhase.Seating && seatPose.Settled)
            {Phase=VisitPhase.Seated;var ready=seated;seated=null;ready?.Invoke();}
            if (head==null && Time.unscaledTime>=nextHeadScan)
            {
                nextHeadScan=Time.unscaledTime+.25f;
                var animator=GetComponentInChildren<Animator>();
                if(animator!=null && animator.isHuman)head=animator.GetBoneTransform(HumanBodyBones.Head);
                if(head==null)
                    foreach(var bone in GetComponentsInChildren<Transform>())
                        if(bone.name=="Head" || bone.name=="Head pivot") { head=bone; break; }
            }
            if(rig!=null && rig.HasShot)
            {
                var eye=anchor.CameraPosition;
                var face=FacePosition;
                eye.y=face.y+.12f;
                rig.RetargetShot(face-Vector3.up*.10f,eye);
                visual.LookAt(rig.ViewCamera.transform,.3f);
            }
        }

        public void End()
        {
            if(!Active)return;
            Active=false;Phase=VisitPhase.None;
            bool stayed=Vector3.Distance(transform.position,returnPosition)<.4f;
            seatPose?.End();
            if(visual!=null){visual.SetSeated(previouslySeated);visual.SetFacing(previousFacing);visual.LookAt(null,0);}
            if(stayed)transform.rotation=returnRotation;
            if(body!=null)body.enabled=colliderEnabled;
            if(agent!=null){agent.updatePosition=updatePosition;agent.updateRotation=updateRotation;}
            if(player!=null)player.ReleaseActivityMove(this);
            stillOpen=null; interrupted=null; seated=null; head=null;
            if(studio!=null)studio.SetActive(false);
        }

        public void RequestExit()
        {
            if(!Active || Phase==VisitPhase.Leaving)return;
            if(Phase==VisitPhase.Approaching || Phase==VisitPhase.Aligning){End();return;}
            Phase=VisitPhase.Leaving;seated=null;
            seatPose.RequestExit();
            if(!seatPose.Active)End();
        }

        private void Interrupt()
        {
            if(!Active)return;
            var notify=interrupted;
            End();
            if(rig!=null)rig.ReleaseShot(.25f);
            notify?.Invoke();
        }

        private void BuildStudio()
        {
            if(studio!=null)Destroy(studio);
            if(backdropMaterial!=null)Destroy(backdropMaterial);
            studio=new GameObject("Diary interview backdrop");
            studio.transform.SetParent(anchor.transform,false);
            var panel=GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name="Upholstered interview wall";
            panel.transform.SetParent(studio.transform,false);
            panel.transform.localPosition=new Vector3(0,1.45f,-.85f);
            panel.transform.localScale=new Vector3(3.6f,2.9f,.10f);
            var collider=panel.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            var shader=Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if(shader!=null)
            {
                backdropMaterial=new Material(shader){name="Diary upholstery",color=new Color(.08f,.14f,.22f)};
                backdropMaterial.SetFloat("_Smoothness",.15f);
                panel.GetComponent<Renderer>().sharedMaterial=backdropMaterial;
            }
            InterviewLight("Diary key",new Vector3(-1.2f,2.2f,1.7f),1.5f,new Color(1f,.93f,.84f));
            InterviewLight("Diary fill",new Vector3(1.4f,1.8f,1.4f),.7f,new Color(.78f,.87f,1f));
        }

        private void InterviewLight(string label,Vector3 at,float intensity,Color colour)
        {
            var lightObject=new GameObject(label);
            lightObject.transform.SetParent(studio.transform,false);
            lightObject.transform.localPosition=at;
            lightObject.transform.localRotation=Quaternion.LookRotation(new Vector3(0,1.2f,0)-at,Vector3.up);
            var light=lightObject.AddComponent<Light>();
            light.type=LightType.Spot;light.spotAngle=70;light.range=5;light.intensity=intensity;light.color=colour;
            light.shadows=LightShadows.None;
        }

        // Teardown must not invoke the director callback: it can rebuild UI during scene unload.
        private void OnDisable()=>End();
        private void OnDestroy()
        {
            End();
            if(studio!=null)Destroy(studio);
            if(backdropMaterial!=null)Destroy(backdropMaterial);
        }
    }
}
