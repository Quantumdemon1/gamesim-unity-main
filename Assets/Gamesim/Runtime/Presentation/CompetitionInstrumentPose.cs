using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Fits hands to a leased instrument after the existing humanoid animation. It never moves
    /// the actor root, hips, feet, collider or agent, and releases its own bone writes on cancel.
    /// Re-querying the animator permits a deferred body/outfit to finish without retaining its rig.
    /// </summary>
    [DefaultExecutionOrder(220)]
    public sealed class CompetitionInstrumentPose : MonoBehaviour
    {
        private CompetitionApparatus instrument;
        private bool holding, pressing;
        private bool fitReady;
        private Func<CompetitionApparatus,bool> fitGate;
        private struct BoneWrite { public Transform bone; public Quaternion before, after; }
        private readonly List<BoneWrite> writes=new List<BoneWrite>(4);
        public bool HasContact { get; private set; }
        public float LeftHandError { get; private set; }
        public float RightHandError { get; private set; }
        private int lastHits,lastRoll;
        private double pressUntil;

        public void SetFitGate(Func<CompetitionApparatus,bool> gate)=>fitGate=gate;

        public void Bind(CompetitionApparatus apparatus,MiniGameRun run,bool active,bool isPaused,bool readyForFit=false)
        {
            instrument=apparatus;
            fitReady=readyForFit;
            holding=active && !isPaused && apparatus!=null && apparatus.Instrument==CompetitionApparatus.Family.GripRig && (run==null || run.Holding);
            if(run!=null && (run.Hits>lastHits || run.RollsUsed>lastRoll))pressUntil=run.Elapsed+.3;
            lastHits=run?.Hits??0;lastRoll=run?.RollsUsed??0;
            pressing=active && !isPaused && run!=null && run.Elapsed<pressUntil;
        }

        private void LateUpdate()
        {
            Restore();HasContact=false;
            if(instrument==null || !fitReady)return;
            if(instrument.Instrument==CompetitionApparatus.Family.PairConsole || instrument.Instrument==CompetitionApparatus.Family.WordConsole)
            {fitGate?.Invoke(instrument);return;}
            Animator animator=GetComponentInChildren<Animator>();
            if(animator==null || !animator.isHuman || !animator.isActiveAndEnabled)
            {fitGate?.Invoke(instrument);return;} // The primitive fallback was fitted from its actual capsule.
            Transform leftArm=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm),leftFore=animator.GetBoneTransform(HumanBodyBones.LeftLowerArm),leftHand=animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform rightArm=animator.GetBoneTransform(HumanBodyBones.RightUpperArm),rightFore=animator.GetBoneTransform(HumanBodyBones.RightLowerArm),rightHand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            if(leftArm==null || leftFore==null || leftHand==null || rightArm==null || rightFore==null || rightHand==null)return;
            // HumanBodyBones.Hand is the wrist, not the hand's gripping/pressing surface.
            // Use a real descendant bone: the middle knuckle grips; the distal finger presses.
            // An incomplete rig falls back to its actual wrist, never an invented reach offset.
            bool grip=instrument.Instrument==CompetitionApparatus.Family.GripRig;
            Transform leftContact=animator.GetBoneTransform(grip?HumanBodyBones.LeftMiddleProximal:HumanBodyBones.LeftMiddleDistal)??leftHand;
            Transform rightContact=animator.GetBoneTransform(grip?HumanBodyBones.RightMiddleProximal:HumanBodyBones.RightMiddleDistal)??rightHand;
            if(instrument.Instrument==CompetitionApparatus.Family.GripRig)
            {
                Transform anchor=instrument.transform.parent;
                Vector3 shoulder=anchor.InverseTransformPoint((leftArm.position+rightArm.position)*.5f);
                float reach=Mathf.Min(Vector3.Distance(leftArm.position,leftFore.position)+Vector3.Distance(leftFore.position,leftHand.position),
                    Vector3.Distance(rightArm.position,rightFore.position)+Vector3.Distance(rightFore.position,rightHand.position));
                CapsuleCollider capsule=GetComponent<CapsuleCollider>();
                float radius=capsule!=null?capsule.radius*Mathf.Max(transform.lossyScale.x,transform.lossyScale.z):.32f;
                instrument.FitGrip(shoulder.y,shoulder.z,reach*.96f,radius,anchor.InverseTransformPoint(transform.position));
            }
            else
            {
                float reach=Vector3.Distance(rightArm.position,rightFore.position)+Vector3.Distance(rightFore.position,rightHand.position);
                instrument.FitPress(instrument.transform.InverseTransformPoint(rightArm.position),reach);
            }
            // Even a hidden rig is prepared after the animator, then its owner checks the actual
            // fitted solids and unowned bodies before activation. Cancellation can release this
            // component from inside the gate, so re-check its binding before writing any bone.
            if(fitGate!=null && !fitGate(instrument))return;
            if(instrument==null || !instrument.isActiveAndEnabled || (!holding && !pressing))return;
            if(holding)
            {
                Transform anchor=instrument.transform.parent;
                LeftHandError=Reach(leftArm,leftFore,leftContact,instrument.HandContact(true),-anchor.right-anchor.up*.4f);
                RightHandError=Reach(rightArm,rightFore,rightContact,instrument.HandContact(false),anchor.right-anchor.up*.4f);
                HasContact=LeftHandError<.035f && RightHandError<.035f;
            }
            else
            {
                RightHandError=Reach(rightArm,rightFore,rightContact,instrument.HandContact(false),instrument.transform.right-instrument.transform.up*.3f);
                HasContact=RightHandError<.035f;
            }
        }

        private float Reach(Transform upper,Transform lower,Transform contact,Vector3 target,Vector3 pole)
        {
            Vector3 origin=upper.position,delta=target-origin;
            float first=Vector3.Distance(origin,lower.position),second=Vector3.Distance(lower.position,contact.position);
            if(first<.03f || second<.03f || delta.sqrMagnitude<.00001f)return float.PositiveInfinity;
            float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(first-second)+.001f,first+second-.001f);
            Vector3 direction=delta.normalized;
            Vector3 bend=Vector3.ProjectOnPlane(pole,direction).normalized;
            if(bend.sqrMagnitude<.5f)bend=Vector3.ProjectOnPlane(Vector3.up,direction).normalized;
            float along=(first*first-second*second+distance*distance)/(2*distance);
            Vector3 elbow=origin+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,first*first-along*along));
            Write(upper,Quaternion.FromToRotation(lower.position-origin,elbow-origin)*upper.rotation);
            Write(lower,Quaternion.FromToRotation(contact.position-lower.position,target-lower.position)*lower.rotation);
            return Vector3.Distance(contact.position,target);
        }

        private void Write(Transform bone,Quaternion worldRotation)
        {
            Quaternion before=bone.localRotation;
            bone.rotation=worldRotation;
            writes.Add(new BoneWrite{bone=bone,before=before,after=bone.localRotation});
        }

        private void Restore()
        {
            // An animator's new frame wins. Undo only an unchanged write that is still ours.
            for(int i=writes.Count-1;i>=0;i--)
            {
                BoneWrite write=writes[i];
                if(write.bone!=null && Quaternion.Angle(write.bone.localRotation,write.after)<.05f)write.bone.localRotation=write.before;
            }
            writes.Clear();
        }

        public void Release()
        {
            Restore();instrument=null;holding=pressing=fitReady=false;fitGate=null;HasContact=false;lastHits=lastRoll=0;pressUntil=0;
        }
        private void OnDisable()=>Release();
        private void OnDestroy()=>Release();
    }
}
