using System;
using Gamesim.House;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>A ceremony-owned NONCONTACT cue at two unchanged standing endpoints.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class FarewellPair : MonoBehaviour
    {
        public enum Phase { Playing, Complete, Cancelled }
        public Phase Stage { get; private set; }
        public bool Released => Stage != Phase.Playing;
        public string ReleaseReason { get; private set; }
        public string FailureDiagnostic { get; private set; }
        public int FiredFrame { get; private set; } = -1;
        public int FirstEnteredFrame { get; private set; } = -1;
        public int SecondEnteredFrame { get; private set; } = -1;
        public bool FirstNeutral { get; private set; }
        public bool SecondNeutral { get; private set; }
        public int LiftedPalmSamples { get; private set; }
        public float MaximumFootDrift { get; private set; }
        public float MaximumRootDrift { get; private set; }
        public float MaximumHeadingDrift { get; private set; }
        public CharacterPresentation First => first?.body;
        public CharacterPresentation Second => second?.body;
        public const float FootTolerance = .001f, RootTolerance = .001f, HeadingTolerance = .05f;

        private sealed class Endpoint
        {
            public CharacterPresentation body;
            public HouseNpcMotion motion;
            public HouseInteractionAnchor place;
            public CharacterPresentation.FarewellBodyIdentity identity;
            public string id, token;
            public Vector3 placeAt, approachAt;
            public float placeYaw;
            public int slot;
            public UnityEngine.SceneManagement.Scene scene;
            public CharacterPresentation.FarewellObservation baseline;
        }
        private Endpoint first, second;
        private HouseMeetingCoordinator coordinator;
        private string generation;
        private Func<bool> current;
        private Func<CharacterPresentation.FarewellObservation, CharacterPresentation.FarewellObservation, bool> visible;
        private float firedAt;

        internal static FarewellPair TryBegin(CharacterPresentation a, CharacterPresentation b, string playerId,
            HouseMeetingCoordinator owner, HouseInteractionAnchor placeA, HouseInteractionAnchor placeB,
            Func<bool> isCurrent,
            Func<CharacterPresentation.FarewellObservation, CharacterPresentation.FarewellObservation, bool> inShot)
        {
            if (a == null || b == null || a == b || a.CharacterId == b.CharacterId || a.CharacterId == playerId || b.CharacterId == playerId
                || owner == null || owner.Generation == null || isCurrent == null || !isCurrent() || inShot == null
                || placeA == null || placeB == null || placeA == placeB || placeA.Slot + placeB.Slot != 1
                || placeA.Slot < 0 || placeA.Slot > 1 || placeB.Slot < 0 || placeB.Slot > 1) return null;
            var left = Capture(a, owner, placeA);
            var right = Capture(b, owner, placeB);
            if (left == null || right == null || a.gameObject.scene != b.gameObject.scene
                || !inShot(left.baseline, right.baseline)) return null;
            var holder = new GameObject("Grounded farewell " + a.CharacterId + " + " + b.CharacterId);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(holder, a.gameObject.scene);
            var pair = holder.AddComponent<FarewellPair>();
            pair.first = left; pair.second = right; pair.coordinator = owner;
            pair.generation = owner.Generation; pair.current = isCurrent; pair.visible = inShot;
            pair.Stage = Phase.Playing; pair.FiredFrame = Time.frameCount; pair.firedAt = Time.unscaledTime;
            // No body turn/translation, new motion token, SetPath or parking operation.
            a.SetFacing(float.NaN); b.SetFacing(float.NaN);
            a.LookAt(null, 0f); b.LookAt(null, 0f);
            a.React(CharacterPresentation.Reaction.Farewell);
            b.React(CharacterPresentation.Reaction.Farewell);
            return pair;
        }

        private static Endpoint Capture(CharacterPresentation body, HouseMeetingCoordinator owner, HouseInteractionAnchor place)
        {
            var seat = body.GetComponent<HouseSeatPresentation>();
            var motion = body.GetComponent<HouseNpcMotion>();
            if (!body.isActiveAndEnabled || body.ReducedMotion || body.IsPrimitiveBody || body.IsBodyAssembling || body.IsChangingOutfit
                || body.IsSeated || seat != null && seat.Active || body.Activity != CharacterPresentation.BodyActivity.None
                || !body.CanBeginFarewell || motion == null || !motion.IsBound || motion.IsParked
                || motion.BoundNpcId != body.CharacterId || motion.LeaseId == null || !owner.CeremonyActorHolds(body.CharacterId)
                || owner.CeremonyPlace(body.CharacterId) != place || !Place(place, body)
                || !motion.HasArrivedAt(motion.LeaseId) || !body.TryGetFarewellBody(out var identity)
                || !body.TryReadFarewell(out var pose) || !pose.Idle) return null;
            return new Endpoint { body = body, motion = motion, place = place, identity = identity,
                id = body.CharacterId, token = motion.LeaseId,
                placeAt = place.Position, approachAt = place.Approach, placeYaw = place.Facing, slot = place.Slot,
                scene = body.gameObject.scene, baseline = pose };
        }

        private static bool Place(HouseInteractionAnchor place, CharacterPresentation body) => place != null
            && place.isActiveAndEnabled && place.gameObject.activeInHierarchy && place.gameObject.scene == body.gameObject.scene
            && place.VenueId == CeremonySeating.HotSeat && place.RoomId == CeremonySets.LivingRoom && place.Posed
            && CharacterPresentation.FiniteFarewell(place.Position) && CharacterPresentation.FiniteFarewell(place.Approach)
            && CharacterPresentation.FiniteFarewell(place.Facing);

        private bool Owns(Endpoint endpoint) => endpoint != null && endpoint.body != null && endpoint.body.isActiveAndEnabled
            && endpoint.body.CharacterId == endpoint.id && endpoint.body.HoldsFarewellBody(endpoint.identity)
            && endpoint.body.gameObject.scene == endpoint.scene && endpoint.place != null && endpoint.place.Slot == endpoint.slot
            && coordinator != null && coordinator.Generation == generation
            && endpoint.motion != null && endpoint.motion.IsBound && endpoint.motion.BoundNpcId == endpoint.id
            && endpoint.motion.LeaseId == endpoint.token && coordinator.CeremonyActorHolds(endpoint.id)
            && coordinator.CeremonyPlace(endpoint.id) == endpoint.place && Place(endpoint.place, endpoint.body)
            && (endpoint.place.Position - endpoint.placeAt).sqrMagnitude <= .000001f
            && (endpoint.place.Approach - endpoint.approachAt).sqrMagnitude <= .000001f
            && Mathf.Abs(Mathf.DeltaAngle(endpoint.place.Facing, endpoint.placeYaw)) <= .01f;

        private bool Read(Endpoint endpoint, out CharacterPresentation.FarewellObservation pose)
        {
            pose = default;
            if (Refused(!Owns(endpoint), "captured endpoint owner/body/place/lease", endpoint)
                || Refused(endpoint.body.ReducedMotion, "reduced motion", endpoint)
                || Refused(endpoint.body.IsBodyAssembling, "body assembling", endpoint)
                || Refused(endpoint.body.IsChangingOutfit, "outfit changing", endpoint)
                || Refused(endpoint.body.IsSeated, "seated", endpoint)
                || Refused(endpoint.body.IsTalking, "talking", endpoint)
                || Refused(endpoint.body.IsArguing, "arguing", endpoint)
                || Refused(endpoint.body.Activity != CharacterPresentation.BodyActivity.None, "activity owner", endpoint)
                || Refused(!endpoint.body.IsStill, "not still", endpoint)
                || Refused(endpoint.motion.IsParked, "parked", endpoint)
                || Refused(!endpoint.motion.HasArrivedAt(endpoint.token), "captured arrival", endpoint)
                || Refused(endpoint.body.GetComponent<HouseSeatPresentation>() is HouseSeatPresentation seat && seat.Active, "active seat", endpoint)
                || Refused(!endpoint.body.TryReadFarewell(out pose), "actual Farewell observation refused", endpoint)) return false;
            MaximumRootDrift = Mathf.Max(MaximumRootDrift, Vector3.Distance(pose.Root, endpoint.baseline.Root));
            MaximumHeadingDrift = Mathf.Max(MaximumHeadingDrift, Quaternion.Angle(pose.Heading, endpoint.baseline.Heading));
            MaximumFootDrift = Mathf.Max(MaximumFootDrift, Vector3.Distance(pose.LeftFoot, endpoint.baseline.LeftFoot),
                Vector3.Distance(pose.RightFoot, endpoint.baseline.RightFoot), Vector3.Distance(pose.LeftToes, endpoint.baseline.LeftToes),
                Vector3.Distance(pose.RightToes, endpoint.baseline.RightToes));
            return !Refused(!(MaximumRootDrift <= RootTolerance), "actual root drift", endpoint, pose)
                && !Refused(!(MaximumHeadingDrift <= HeadingTolerance), "actual heading drift", endpoint, pose)
                && !Refused(!(MaximumFootDrift <= FootTolerance), "actual foot drift", endpoint, pose);
        }

        // Only the first already-refused predicate reads diagnostic fields. Never reread a pose, advance
        // an Animator, consume a cue, reset a baseline or repeat the current/visible delegates.
        private bool Refused(bool refused, string reason, Endpoint endpoint = null,
            CharacterPresentation.FarewellObservation? observed = null)
        {
            if (!refused || FailureDiagnostic != null) return refused;
            var report = new System.Text.StringBuilder(1024);
            try
            {
                report.Append("frame=").Append(Time.frameCount).Append(" delta=").Append(DiagnosticFloat(Time.deltaTime))
                    .Append(" scale=").Append(DiagnosticFloat(Time.timeScale)).Append(" reason=").Append(reason)
                    .Append(" endpoint=").Append(endpoint == first ? "first" : endpoint == second ? "second" : "pair")
                    .Append(" capturedGeneration=").Append(DiagnosticText(generation, 64))
                    .Append(" currentGeneration=").Append(DiagnosticText(coordinator != null ? coordinator.Generation : null, 64));
                if (endpoint != null)
                {
                    report.Append(" capturedId=").Append(DiagnosticText(endpoint.id, 64))
                        .Append(" capturedLease=").Append(DiagnosticText(endpoint.token, 64))
                        .Append(" currentLease=").Append(DiagnosticText(endpoint.motion != null ? endpoint.motion.LeaseId : null, 64))
                        .Append(" capturedBody=").Append(DiagnosticId(endpoint.identity.Animator))
                        .Append("/").Append(DiagnosticId(endpoint.identity.Controller)).Append("/").Append(DiagnosticId(endpoint.identity.Avatar))
                        .Append(" revision=").Append(endpoint.identity.RequestRevision);
                    if (observed.HasValue)
                    {
                        var pose = observed.Value; var baseline = endpoint.baseline;
                        report.Append(" inTake=").Append(pose.InTake).Append(" idle=").Append(pose.Idle)
                            .Append(" boundary=").Append(pose.Boundary).Append(" progress=").Append(DiagnosticFloat(pose.NormalizedTime))
                            .Append(" weight=").Append(DiagnosticFloat(pose.SelectedClipWeight))
                            .Append(" rootGap=").Append(DiagnosticFloat(Vector3.Distance(baseline.Root, pose.Root)))
                            .Append(" headingGap=").Append(DiagnosticFloat(Quaternion.Angle(baseline.Heading, pose.Heading)));
                        void Foot(string name, Vector3 before, Vector3 now) => report.Append(" ").Append(name)
                            .Append("Base=").Append(DiagnosticVector(before)).Append(" Now=").Append(DiagnosticVector(now))
                            .Append(" Gap=").Append(DiagnosticFloat(Vector3.Distance(before, now)));
                        Foot("LeftFoot", baseline.LeftFoot, pose.LeftFoot); Foot("RightFoot", baseline.RightFoot, pose.RightFoot);
                        Foot("LeftToes", baseline.LeftToes, pose.LeftToes); Foot("RightToes", baseline.RightToes, pose.RightToes);
                    }
                    report.Append(" ").Append(DiagnosticNative(endpoint.body, 768));
                }
                else report.Append(" first{").Append(DiagnosticNative(first?.body, 640))
                    .Append("} second{").Append(DiagnosticNative(second?.body, 640)).Append("}");
            }
            catch (Exception error) { report.Append(" diagnostic unavailable: ").Append(error.GetType().Name); }
            FailureDiagnostic = DiagnosticText(report.ToString(), 2048);
            return refused;
        }

        internal static string DiagnosticText(string text, int cap) => text == null ? "null"
            : text.Length <= cap ? text : text.Substring(0, cap);
        internal static string DiagnosticFloat(float value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        internal static string DiagnosticVector(Vector3 value) => "(" + DiagnosticFloat(value.x) + "," + DiagnosticFloat(value.y) + "," + DiagnosticFloat(value.z) + ")";
        internal static string DiagnosticId(UnityEngine.Object value) => value == null ? "null" : value.GetEntityId().ToString();
        internal static string DiagnosticNative(CharacterPresentation body, int cap)
        {
            var report = new System.Text.StringBuilder(512);
            try
            {
                if (body == null) return "body=null";
                report.Append("actor=").Append(DiagnosticText(body.CharacterId, 64)).Append(" body=").Append(DiagnosticId(body))
                    .Append(" root=").Append(DiagnosticVector(body.transform.position))
                    .Append(" still=").Append(body.IsStill).Append(" seated=").Append(body.IsSeated).Append(" activity=").Append(body.Activity);
                var live = body.VisualRoot != null ? body.VisualRoot.GetComponentInChildren<Animator>(true) : null;
                report.Append(" animator=").Append(DiagnosticId(live));
                if (live != null)
                {
                    report.Append(" controller=").Append(DiagnosticId(live.runtimeAnimatorController))
                        .Append(" avatar=").Append(DiagnosticId(live.avatar)).Append(" initialized=").Append(live.isInitialized);
                    if (live.isInitialized && live.runtimeAnimatorController != null && live.layerCount > 0)
                    {
                        var state = live.GetCurrentAnimatorStateInfo(0); bool transition = live.IsInTransition(0);
                        var next = transition ? live.GetNextAnimatorStateInfo(0) : default;
                        report.Append(" current=").Append(state.fullPathHash).Append("/").Append(DiagnosticFloat(state.normalizedTime))
                            .Append(" transition=").Append(transition).Append(" next=").Append(next.fullPathHash).Append("/").Append(DiagnosticFloat(next.normalizedTime));
                        void Clips(string label, AnimatorClipInfo[] clips)
                        {
                            report.Append(" ").Append(label).Append("Count=").Append(clips.Length);
                            for (int i = 0; i < clips.Length && i < 4; i++) report.Append(" [")
                                .Append(DiagnosticText(clips[i].clip != null ? clips[i].clip.name : null, 64))
                                .Append("#").Append(DiagnosticId(clips[i].clip)).Append(" weight=").Append(DiagnosticFloat(clips[i].weight)).Append("]");
                        }
                        Clips("current", live.GetCurrentAnimatorClipInfo(0));
                        if (transition) Clips("next", live.GetNextAnimatorClipInfo(0));
                    }
                }
            }
            catch (Exception error) { report.Append(" diagnostic unavailable: ").Append(error.GetType().Name); }
            return DiagnosticText(report.ToString(), cap);
        }

        private void LateUpdate()
        {
            if (Stage != Phase.Playing || first == null) return;
            if (Refused(Time.timeScale <= 0f, "timeScale") || Refused(Time.deltaTime <= 0f, "deltaTime")
                || Refused(current == null, "current delegate missing") || Refused(!current(), "current owner delegate")
                || !Read(first, out var a) || !Read(second, out var b)) { Cancel("standing owner/body/clock/feet changed"); return; }
            if (Refused(!visible(a, b), "actual visible delegate", first, a)) { Cancel("actual gesture endpoints left the shot"); return; }
            if (a.InTake && FirstEnteredFrame < 0) FirstEnteredFrame = Time.frameCount;
            if (b.InTake && SecondEnteredFrame < 0) SecondEnteredFrame = Time.frameCount;
            if (FirstEnteredFrame >= 0 && SecondEnteredFrame >= 0 && FirstEnteredFrame != SecondEnteredFrame)
            { Cancel("the actual takes did not enter on the same frame"); return; }
            FirstNeutral |= a.InTake && a.NormalizedTime >= CharacterPresentation.FarewellNeutral;
            SecondNeutral |= b.InTake && b.NormalizedTime >= CharacterPresentation.FarewellNeutral;
            // These are actual animated hand samples, NOT predictions made before firing.
            if (a.InTake && b.InTake && a.NormalizedTime >= 10f / 30f && a.NormalizedTime <= 17f / 30f
                && b.NormalizedTime >= 10f / 30f && b.NormalizedTime <= 17f / 30f
                && a.RightHand.y > first.baseline.RightHand.y + .03f && b.RightHand.y > second.baseline.RightHand.y + .03f)
                LiftedPalmSamples++;
            if (FirstEnteredFrame >= 0 && SecondEnteredFrame >= 0 && a.Idle && b.Idle)
            {
                if (!FirstNeutral || !SecondNeutral || LiftedPalmSamples == 0) { Cancel("actual neutral/palm interval not observed"); return; }
                Stage = Phase.Complete; ReleaseReason = "both actual takes returned to idle";
                return;
            }
            float elapsed = Time.unscaledTime - firedAt;
            if ((!a.InTake && !a.Idle && !OwnBoundary(a, FirstEnteredFrame, FirstNeutral, elapsed))
                || (!b.InTake && !b.Idle && !OwnBoundary(b, SecondEnteredFrame, SecondNeutral, elapsed)))
            { Cancel("an unrelated state or unqualified boundary interrupted the take"); return; }
            if (Time.unscaledTime - firedAt > (FirstEnteredFrame < 0 || SecondEnteredFrame < 0 ? .5f : 2f))
                Cancel("actual take entry/completion deadline elapsed");
        }

        private static bool OwnBoundary(CharacterPresentation.FarewellObservation pose, int enteredFrame, bool neutral, float elapsed)
            => pose.Boundary == CharacterPresentation.FarewellBoundary.IncomingZeroWeight
                ? enteredFrame < 0 && elapsed <= .5f
                : pose.Boundary == CharacterPresentation.FarewellBoundary.OutgoingZeroWeight
                    && enteredFrame >= 0 && neutral && elapsed <= 2f;

        public void Cancel(string reason)
        {
            if (Stage != Phase.Playing) return;
            Stage = Phase.Cancelled; ReleaseReason = reason;
            // Clear state before calling presentation. Never touch replacement/foreign-owned bodies.
            if (Owns(first)) first.body.CancelOwnedFarewell(first.identity);
            if (Owns(second)) second.body.CancelOwnedFarewell(second.identity);
        }
        private void OnDisable() => Cancel("farewell holder disabled");
        private void OnDestroy() => Cancel("farewell holder destroyed");
    }
}
