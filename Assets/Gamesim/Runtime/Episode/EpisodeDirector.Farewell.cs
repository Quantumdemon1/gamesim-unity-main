using Gamesim.House;
using Gamesim.Presentation;
using UnityEngine;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeDirector
    {
        private FarewellPair goodbyeFarewell;
        /// <summary>The actual NONCONTACT transaction; never an alias for GoodbyeEmbrace.</summary>
        public FarewellPair GoodbyeFarewell => goodbyeFarewell;
        public string GoodbyeFarewellFailure => ceremonyStage?.farewellFailure;
        public string GoodbyeFarewellDiagnostic => ceremonyStage?.FarewellDiagnostic;
        public int GoodbyeFarewellStandingObservations => ceremonyStage?.FarewellStandingObservations ?? 0;
        public int GoodbyeFarewellCameraCutFrame => ceremonyStage?.FarewellCameraCutFrame ?? -1;
        public int GoodbyeFarewellIdleObservations => ceremonyStage?.FarewellIdleObservations ?? 0;
        public int GoodbyeFarewellIdlePreparationAttempts => ceremonyStage?.FarewellIdlePreparationAttempts ?? 0;
        public int GoodbyeFarewellIdlePreparationCalls => ceremonyStage?.FarewellIdlePreparationCalls ?? 0;
        public int GoodbyeFarewellIdlePreparationFrame => ceremonyStage?.FarewellIdlePreparationFrame ?? -1;
        public int GoodbyeFarewellIdleObservedFrame => ceremonyStage?.FarewellIdleObservedFrame ?? -1;
#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
        /// <summary>Controlled test boundary only; not an ordinary episode/UI action or a shipping API.</summary>
        public bool ArmGoodbyeFarewellAfterFirstWriteForTests(System.Action<CharacterPresentation, CharacterPresentation, HouseMeetingCoordinator, HouseInteractionAnchor, HouseInteractionAnchor> boundary)
            => ceremonyStage != null && ceremonyStage.ArmFarewellAfterFirstWriteForTests(boundary);
        public void ClearGoodbyeFarewellAfterFirstWriteForTests()
            => ceremonyStage?.ClearFarewellAfterFirstWriteForTests();
#endif
        private void ReleaseGoodbyeFarewell(string reason)
        {
            if (ceremonyStage != null) ceremonyStage.EndTheGoodbyeFarewell(reason);
            else if (goodbyeFarewell != null) { goodbyeFarewell.Cancel(reason); Destroy(goodbyeFarewell.gameObject); goodbyeFarewell = null; }
        }

        private sealed partial class CeremonyStage
        {
            private HouseMeetingCoordinator farewellOwner;
            private CharacterPresentation farewellFirst, farewellSecond;
            private HouseNpcMotion farewellMotionA, farewellMotionB;
            private HouseInteractionAnchor farewellPlaceA, farewellPlaceB;
            private Transform farewellVisualA, farewellVisualB;
            private CharacterPresentation.FarewellBodyIdentity farewellBodyA, farewellBodyB;
            private Vector3 farewellPlaceAtA, farewellPlaceAtB, farewellApproachA, farewellApproachB;
            private float farewellYawA, farewellYawB;
            private string farewellGeneration, farewellTokenA, farewellTokenB;
            private long farewellLoad;
            private bool farewellPrimitive, farewellFramed, farewellFaceRestored;
            private HouseCameraRig.Shot farewellShot;
            private HouseCameraRig farewellRig;
            private Camera farewellCamera;
            private UnityEngine.SceneManagement.Scene farewellScene;
            private int farewellSlotA, farewellSlotB;
            private int farewellCutFrame = -1, farewellReadyFrame = -1, farewellReadyFrames;
            private bool farewellIdlePrepared, farewellIdleAttemptUsed;
            private int farewellIdlePreparationFrame = -1, farewellIdleFrame = -1, farewellIdleFrames, farewellIdleCalls;
            private float farewellIdlePhaseA, farewellIdlePhaseB;
            private CharacterPresentation.FarewellIdlePreparation farewellIdleA, farewellIdleB;
            public string farewellFailure;
            private string farewellFirstRefusal, farewellTerminalRefusal, farewellLastRefusal, farewellFirstViewportRefusal;
            public string FarewellDiagnostic => farewellTerminalRefusal ?? farewellFirstRefusal;
            public int FarewellStandingObservations => farewellReadyFrames;
            public int FarewellCameraCutFrame => farewellCutFrame;
            public int FarewellIdleObservations => farewellIdleFrames;
            public int FarewellIdlePreparationAttempts => farewellIdleAttemptUsed ? 1 : 0;
            public int FarewellIdlePreparationCalls => farewellIdleCalls;
            public int FarewellIdlePreparationFrame => farewellIdlePreparationFrame;
            public int FarewellIdleObservedFrame => farewellIdleFrame;
#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
            private System.Action<CharacterPresentation, CharacterPresentation, HouseMeetingCoordinator, HouseInteractionAnchor, HouseInteractionAnchor> farewellAfterFirstWriteForTests;
            public bool ArmFarewellAfterFirstWriteForTests(System.Action<CharacterPresentation, CharacterPresentation, HouseMeetingCoordinator, HouseInteractionAnchor, HouseInteractionAnchor> boundary)
            {
                if (boundary == null || farewellAfterFirstWriteForTests != null || !Active
                    || Step != CeremonyStageStep.Goodbye || farewellOwner == null || farewellPrimitive
                    || farewellIdlePreparationFrame >= 0) return false;
                farewellAfterFirstWriteForTests = boundary;
                return true;
            }
            public void ClearFarewellAfterFirstWriteForTests() => farewellAfterFirstWriteForTests = null;
#endif
            private readonly RaycastHit[] farewellBoomHits = new RaycastHit[16];

            private bool PrepareTheGoodbyeFarewell(string id)
            {
                if (survivorId == null || survivorId == id || id == state.playerId || survivorId == state.playerId
                    || LetGo(id) || LetGo(survivorId) || director.npcMeetings == null) return false;
                var a = Visual(id); var b = Visual(survivorId);
                var owner = director.npcMeetings;
                var pa = owner.CeremonyPlace(id); var pb = owner.CeremonyPlace(survivorId);
                if (a == null || b == null || a == b || pa == null || pb == null || pa == pb
                    || !owner.CeremonyActorHolds(id) || !owner.CeremonyActorHolds(survivorId)
                    || pa.VenueId != CeremonySeating.HotSeat || pb.VenueId != CeremonySeating.HotSeat
                    || pa.RoomId != CeremonySets.LivingRoom || pb.RoomId != CeremonySets.LivingRoom
                    || pa.Slot < 0 || pa.Slot > 1 || pb.Slot < 0 || pb.Slot > 1 || pa.Slot == pb.Slot
                    || pa.gameObject.scene != a.gameObject.scene || pb.gameObject.scene != b.gameObject.scene
                    || a.gameObject.scene != b.gameObject.scene || a.IsPrimitiveBody != b.IsPrimitiveBody) return false;
                var ma = a.GetComponent<HouseNpcMotion>(); var mb = b.GetComponent<HouseNpcMotion>();
                if (ma == null || mb == null || !ma.IsBound || !mb.IsBound || ma.BoundNpcId != id || mb.BoundNpcId != survivorId
                    || ma.LeaseId == null || mb.LeaseId == null || owner.Generation == null
                    || !pa.isActiveAndEnabled || !pb.isActiveAndEnabled || !pa.Posed || !pb.Posed
                    || !CharacterPresentation.FiniteFarewell(pa.Position) || !CharacterPresentation.FiniteFarewell(pb.Position)
                    || !CharacterPresentation.FiniteFarewell(pa.Approach) || !CharacterPresentation.FiniteFarewell(pb.Approach)
                    || !CharacterPresentation.FiniteFarewell(pa.Facing) || !CharacterPresentation.FiniteFarewell(pb.Facing)) return false;
                farewellPrimitive = a.IsPrimitiveBody && b.IsPrimitiveBody;
                if (!farewellPrimitive && (!a.TryGetFarewellBody(out farewellBodyA) || !b.TryGetFarewellBody(out farewellBodyB))) return false;
                farewellOwner = owner; farewellGeneration = owner.Generation; farewellLoad = director.loadGeneration;
                farewellFirst = a; farewellSecond = b; farewellMotionA = ma; farewellMotionB = mb;
                farewellPlaceA = pa; farewellPlaceB = pb; farewellTokenA = ma.LeaseId; farewellTokenB = mb.LeaseId;
                farewellVisualA = a.VisualRoot; farewellVisualB = b.VisualRoot;
                farewellPlaceAtA = pa.Position; farewellPlaceAtB = pb.Position;
                farewellApproachA = pa.Approach; farewellApproachB = pb.Approach;
                farewellYawA = pa.Facing; farewellYawB = pb.Facing;
                farewellSlotA = pa.Slot; farewellSlotB = pb.Slot; farewellScene = a.gameObject.scene;
                a.SetTalking(false); b.SetTalking(false); a.SetArguing(false); b.SetArguing(false);
                a.LookAt(null, 0f); b.LookAt(null, 0f);
                return true;
            }

            private bool FarewellOwned() => Active && Step == CeremonyStageStep.Goodbye
                && ReferenceEquals(director.ceremonyStage, this) && director.loadGeneration == farewellLoad
                && director.departingId == leavingId && !director.reducedMotion && !director.npcWorldFailed
                && ReferenceEquals(director.npcMeetings, farewellOwner) && farewellOwner != null
                && farewellOwner.Generation == farewellGeneration && farewellFirst != null && farewellSecond != null
                && Visual(leavingId) == farewellFirst && Visual(survivorId) == farewellSecond
                && farewellFirst.CharacterId == leavingId && farewellSecond.CharacterId == survivorId
                && farewellFirst.gameObject.scene == farewellScene && farewellSecond.gameObject.scene == farewellScene
                && farewellFirst.VisualRoot == farewellVisualA && farewellSecond.VisualRoot == farewellVisualB
                && (farewellPrimitive ? farewellFirst.IsPrimitiveBody && farewellSecond.IsPrimitiveBody
                    : farewellFirst.HoldsFarewellBody(farewellBodyA) && farewellSecond.HoldsFarewellBody(farewellBodyB))
                && farewellOwner.CeremonyPlace(leavingId) == farewellPlaceA && farewellOwner.CeremonyPlace(survivorId) == farewellPlaceB
                && farewellOwner.CeremonyActorHolds(leavingId) && farewellOwner.CeremonyActorHolds(survivorId)
                && farewellMotionA != null && farewellMotionB != null
                && farewellFirst.GetComponent<HouseNpcMotion>() == farewellMotionA && farewellSecond.GetComponent<HouseNpcMotion>() == farewellMotionB
                && farewellMotionA.IsBound && farewellMotionB.IsBound && farewellMotionA.BoundNpcId == leavingId && farewellMotionB.BoundNpcId == survivorId
                && farewellMotionA.LeaseId == farewellTokenA && farewellMotionB.LeaseId == farewellTokenB
                && PlacesUnchanged();

            private bool PlacesUnchanged() => farewellPlaceA != null && farewellPlaceB != null
                && farewellPlaceA.isActiveAndEnabled && farewellPlaceB.isActiveAndEnabled
                && farewellPlaceA.gameObject.scene == farewellScene && farewellPlaceB.gameObject.scene == farewellScene
                && farewellPlaceA.Posed && farewellPlaceB.Posed && farewellPlaceA.RoomId == CeremonySets.LivingRoom
                && farewellPlaceB.RoomId == CeremonySets.LivingRoom && farewellPlaceA.VenueId == CeremonySeating.HotSeat
                && farewellPlaceB.VenueId == CeremonySeating.HotSeat && farewellPlaceA.Slot == farewellSlotA && farewellPlaceB.Slot == farewellSlotB
                && (farewellPlaceA.Position - farewellPlaceAtA).sqrMagnitude <= .000001f
                && (farewellPlaceB.Position - farewellPlaceAtB).sqrMagnitude <= .000001f
                && (farewellPlaceA.Approach - farewellApproachA).sqrMagnitude <= .000001f
                && (farewellPlaceB.Approach - farewellApproachB).sqrMagnitude <= .000001f
                && Mathf.Abs(Mathf.DeltaAngle(farewellPlaceA.Facing, farewellYawA)) <= .01f
                && Mathf.Abs(Mathf.DeltaAngle(farewellPlaceB.Facing, farewellYawB)) <= .01f;

            private bool FarewellStanding()
            {
                var sa = SeatOf(leavingId); var sb = SeatOf(survivorId);
                return (sa == null || !sa.Active) && (sb == null || !sb.Active)
                    && !farewellMotionA.IsParked && !farewellMotionB.IsParked
                    && farewellMotionA.HasArrivedAt(farewellTokenA) && farewellMotionB.HasArrivedAt(farewellTokenB)
                    && !farewellFirst.IsSeated && !farewellSecond.IsSeated && farewellFirst.IsStill && farewellSecond.IsStill;
            }

            private void TickTheGoodbyeFarewell(float now)
            {
                if (farewellOwner == null) return;
                if (ReadinessRefused(!FarewellOwned(), "captured owner") || ReadinessRefused(Time.timeScale <= 0f, "timeScale")) { EndTheGoodbyeFarewell("farewell owner/clock unavailable"); return; }
                if (director.goodbyeFarewell != null)
                {
                    if (director.goodbyeFarewell.Stage == FarewellPair.Phase.Cancelled)
                    { farewellFailure = director.goodbyeFarewell.ReleaseReason;
                        if (farewellTerminalRefusal == null) farewellTerminalRefusal = FarewellPair.DiagnosticText(director.goodbyeFarewell.FailureDiagnostic, 2048);
                        RestoreFarewellFace(); }
                    else if (director.goodbyeFarewell.Stage == FarewellPair.Phase.Complete) RestoreFarewellFace();
                    return;
                }
                if (!farewellAtGoodbye) return;
                if (now > farewellAtGoodbyeBy) { EndTheGoodbyeFarewell("standing/framing readiness exceeded1.5s"); return; }
                if (ReadinessRefused(!FarewellStanding(), "standing"))
                { farewellReadyFrames = 0; farewellReadyFrame = -1; ResetFarewellIdleFrames(); return; }
                if (farewellReadyFrame != Time.frameCount)
                {
                    farewellReadyFrames = farewellReadyFrame == Time.frameCount - 1 ? farewellReadyFrames + 1 : 1;
                    farewellReadyFrame = Time.frameCount;
                }
                // Own successive post-unpark observations; the motion's older arrival counter is NOT reused as new evidence.
                if (farewellReadyFrames < 2) return;
                if (farewellPrimitive)
                {
                    farewellAtGoodbye = false;
                    farewellFirst.SetFacing(float.NaN); farewellSecond.SetFacing(float.NaN);
                    farewellFirst.React(CharacterPresentation.Reaction.Farewell); farewellSecond.React(CharacterPresentation.Reaction.Farewell);
                    return; // Explicit stand-in diagnostic only; no playback/foot/framing claim.
                }
                if (!farewellIdlePrepared)
                {
                    PrepareTheOwnedFarewellIdle();
                    return; // New observations must be on naturally evaluated frames AFTER this authorization.
                }
                if (!ObserveTheOwnedFarewellIdle()) return;
                if (ReadinessRefused(!farewellFirst.CanBeginFarewell, "first CanBeginFarewell")
                    || ReadinessRefused(!farewellSecond.CanBeginFarewell, "second CanBeginFarewell")
                    || ReadinessRefused(!farewellFirst.TryReadFarewell(out var a), "first actual Farewell read")
                    || ReadinessRefused(!farewellSecond.TryReadFarewell(out var b), "second actual Farewell read")) return;
                if (!farewellFramed)
                {
                    var shot = Wide(CutSeconds);
                    shot.Focus = (a.Head + b.Head) * .5f - Vector3.up * .3f;
                    if (!FarewellBoomOpen(shot)) { EndTheGoodbyeFarewell("farewell shot boom refused"); return; }
                    farewellShot = shot;
                    farewellRig = director.cameraRig; farewellCamera = farewellRig != null ? farewellRig.ViewCamera : null;
                    Cut(shot); farewellFramed = true; farewellCutFrame = Time.frameCount;
                    return; // Actual camera, not desired focus, must settle before its projection is read.
                }
                if (ReadinessRefused(Time.frameCount <= farewellCutFrame, "camera later-frame boundary")
                    || !FarewellCameraReady() || !FarewellVisible(a, b)) return;
                director.goodbyeFarewell = FarewellPair.TryBegin(farewellFirst, farewellSecond, state.playerId,
                    farewellOwner, farewellPlaceA, farewellPlaceB, FarewellOwned, FarewellVisible);
                if (ReadinessRefused(director.goodbyeFarewell == null, "actual TryBegin returned null")) return;
                farewellAtGoodbye = false;
            }

            private void ResetFarewellIdleFrames()
            { farewellIdleFrame = -1; farewellIdleFrames = 0; }

            private void PrepareTheOwnedFarewellIdle()
            {
                try
                {
                    if (farewellIdleAttemptUsed) { EndTheGoodbyeFarewell("owned Idle preparation already attempted"); return; }
                    // BOTH read-only preflights precede ANY Animator write. An existing transition is waited upon.
                    if (ReadinessRefused(!FarewellOwned() || !FarewellStanding(), "Idle preparation captured owner")
                        || ReadinessRefused(!farewellFirst.TryCaptureFarewellIdle(farewellBodyA, out var a), "first exact Idle/WalkStop preparation")
                        || ReadinessRefused(!farewellSecond.TryCaptureFarewellIdle(farewellBodyB, out var b), "second exact Idle/WalkStop preparation")
                        || ReadinessRefused(!farewellFirst.HoldsFarewellIdlePreparation(a)
                            || !farewellSecond.HoldsFarewellIdlePreparation(b), "dual Idle preparation changed")) return;
                    farewellIdleA = a; farewellIdleB = b;
                    farewellIdlePreparationFrame = Time.frameCount;
                    ResetFarewellIdleFrames();
                    farewellIdleAttemptUsed = !a.WasIdle || !b.WasIdle; // Stamp BEFORE an engine call, including partial failure.
                    if (!FarewellOwned() || !FarewellStanding()
                        || !farewellFirst.HoldsFarewellIdlePreparation(a) || !farewellSecond.HoldsFarewellIdlePreparation(b))
                    { EndTheGoodbyeFarewell("Idle preparation lost dual authority before commit"); return; }
                    if (!farewellFirst.PrepareCapturedFarewellIdle(a))
                    { EndTheGoodbyeFarewell("first Idle preparation refused"); return; }
                    if (!a.WasIdle)
                    {
                        farewellIdleCalls++;
#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
                        // AFTER a normally returned FIRST WalkStop write, BEFORE the original SECOND recheck.
                        // Clear BEFORE invocation; a callback interruption is controlled test evidence only.
                        var boundary = farewellAfterFirstWriteForTests;
                        farewellAfterFirstWriteForTests = null;
                        boundary?.Invoke(farewellFirst, farewellSecond, farewellOwner, farewellPlaceA, farewellPlaceB);
#endif
                    }
                    // Native writes are NOT atomic. Recheck the whole owner/SECOND record and FIRST's
                    // exact allowed presentation before the second call; never steal a replacement/foreign cue.
                    if (!FarewellOwned() || !FarewellStanding() || !farewellSecond.HoldsFarewellIdlePreparation(b)
                        || !farewellFirst.HoldsFarewellIdleAfterPreparation(a))
                    { EndTheGoodbyeFarewell("Idle preparation interrupted after first endpoint"); return; }
                    if (!farewellSecond.PrepareCapturedFarewellIdle(b))
                    { EndTheGoodbyeFarewell("second Idle preparation refused"); return; }
                    if (!b.WasIdle) farewellIdleCalls++;
                    if (!FarewellOwned() || !FarewellStanding()
                        || !farewellFirst.HoldsFarewellIdleAfterPreparation(a) || !farewellSecond.HoldsFarewellIdleAfterPreparation(b))
                    { EndTheGoodbyeFarewell("Idle preparation lost authority after commit"); return; }
                    farewellIdlePrepared = true;
                }
                catch (System.Exception error)
                { EndTheGoodbyeFarewell("owned Idle preparation failure: " + error.GetType().Name); }
                // NO Animator rollback/cancel is performed here. Original captured-lease cleanup stays exact.
            }

            private bool ObserveTheOwnedFarewellIdle()
            {
                try
                {
                    if (Time.frameCount <= farewellIdlePreparationFrame) return false;
                    if (ReadinessRefused(!FarewellOwned() || !FarewellStanding(), "post-preparation Idle owner")
                        || ReadinessRefused(!farewellFirst.TryCaptureFarewellIdle(farewellBodyA, out var a)
                            || !a.WasIdle || a.IdleMotion != farewellIdleA.IdleMotion, "first actual settled Idle")
                        || ReadinessRefused(!farewellSecond.TryCaptureFarewellIdle(farewellBodyB, out var b)
                            || !b.WasIdle || b.IdleMotion != farewellIdleB.IdleMotion, "second actual settled Idle"))
                    { ResetFarewellIdleFrames(); return false; }
                    if (farewellIdleFrame == Time.frameCount) return farewellIdleFrames >= 2;
                    // A frame number alone could describe an unevaluated/cull-stalled Animator.
                    // No-op Idle must advance beyond its captured phase; two counted samples must advance BOTH clips.
                    if (farewellIdleA.WasIdle && a.Phase <= farewellIdleA.Phase
                        || farewellIdleB.WasIdle && b.Phase <= farewellIdleB.Phase)
                    { ResetFarewellIdleFrames(); return false; }
                    bool successive = farewellIdleFrame == Time.frameCount - 1
                        && a.Phase > farewellIdlePhaseA && b.Phase > farewellIdlePhaseB;
                    farewellIdleFrames = successive ? farewellIdleFrames + 1 : 1;
                    farewellIdleFrame = Time.frameCount; farewellIdlePhaseA = a.Phase; farewellIdlePhaseB = b.Phase;
                    return farewellIdleFrames >= 2;
                }
                catch (System.Exception error)
                { EndTheGoodbyeFarewell("owned Idle observation failure: " + error.GetType().Name); return false; }
            }

            private bool FarewellCameraReady()
            {
                var rig = director.cameraRig;
                var camera = rig != null ? rig.ViewCamera : null;
                if (ReadinessRefused(!FarewellOwned(), "camera captured owner")
                    || ReadinessRefused(!holdsCamera, "camera lease")
                    || ReadinessRefused(camera == null, "actual camera missing")
                    || ReadinessRefused(!camera.isActiveAndEnabled, "actual camera disabled")
                    || ReadinessRefused(!rig.HasShot, "actual shot missing")
                    || ReadinessRefused(rig != farewellRig, "camera rig replaced")
                    || ReadinessRefused(camera != farewellCamera, "camera replaced")
                    || ReadinessRefused(rig.IsTravelling, "camera travelling")
                    || ReadinessRefused(rig.LensOrthographic != 0f, "rig orthographic")
                    || ReadinessRefused(camera.orthographic, "camera orthographic")
                    || ReadinessRefused(!CharacterPresentation.FiniteFarewell(camera.fieldOfView), "nonfinite field of view")
                    || ReadinessRefused(Mathf.Abs(camera.fieldOfView - Lens) > .01f, "field of view not settled")
                    || ReadinessRefused(!CharacterPresentation.FiniteFarewell(camera.aspect), "nonfinite aspect")
                    || ReadinessRefused(camera.aspect <= 0f, "nonpositive aspect")) return false;
                var rotation = Quaternion.Euler(farewellShot.Pitch, farewellShot.Yaw, 0f);
                var eye = farewellShot.Focus + rotation * Vector3.back * farewellShot.Distance;
                if (ReadinessRefused(Vector3.Distance(camera.transform.position, eye) > .01f, "actual eye not settled")
                    || ReadinessRefused(Quaternion.Angle(camera.transform.rotation, rotation) > .05f, "actual camera rotation not settled")
                    || ReadinessRefused(Mathf.Abs(rig.AppliedDistance - farewellShot.Distance) > .01f, "actual applied distance not settled")) return false;
                var projection = camera.projectionMatrix;
                for (int i = 0; i < 16; i++) if (!CharacterPresentation.FiniteFarewell(projection[i]))
                { ReadinessRefused(true, "nonfinite projection index=" + i); return false; }
                return true; // Actual later-frame pose/lens AND actual viewport checks; desired focus is not proof.
            }

            private bool FarewellBoomOpen(HouseCameraRig.Shot shot)
            {
                var back = Quaternion.Euler(shot.Pitch, shot.Yaw, 0f) * Vector3.back;
                int count = Physics.SphereCastNonAlloc(shot.Focus + back * .4f, .18f, back, farewellBoomHits,
                    shot.Distance - .4f, HouseLayers.Sight, QueryTriggerInteraction.Ignore);
                if (count == farewellBoomHits.Length) return false;
                for (int i = 0; i < count; i++)
                {
                    var hit = farewellBoomHits[i];
                    if (hit.distance <= 0f || hit.transform.GetComponentInParent<HouseNpc>() != null
                        || hit.transform.GetComponentInParent<HousePlayerController>() != null) continue;
                    return false;
                }
                return true;
            }

            private bool FarewellVisible(CharacterPresentation.FarewellObservation a, CharacterPresentation.FarewellObservation b)
            {
                var camera = director.cameraRig != null ? director.cameraRig.ViewCamera : null;
                if (!FarewellCameraReady()) return false;
                Vector3 rejectedViewport = default;
                bool Point(Vector3 at)
                {
                    var p = camera.WorldToViewportPoint(at);
                    bool accepted = CharacterPresentation.FiniteFarewell(p) && p.z > camera.nearClipPlane
                        && p.z < camera.farClipPlane && p.x >= .05f && p.x <= .95f && p.y >= .05f && p.y <= .95f;
                    if (!accepted) rejectedViewport = p;
                    return accepted;
                }
                int endpoint = 0;
                var landmarkNames = new[] { "Head", "LeftHand", "RightHand", "LeftFoot", "RightFoot", "LeftToes", "RightToes" };
                foreach (var pose in new[] { a, b })
                {
                    int landmark = 0;
                    foreach (var p in new[] { pose.Head, pose.LeftHand, pose.RightHand, pose.LeftFoot, pose.RightFoot, pose.LeftToes, pose.RightToes })
                    {
                        if (!Point(p)) { ViewportRefused("endpoint=" + endpoint + " landmark=" + landmarkNames[landmark], rejectedViewport, camera); return false; }
                        landmark++;
                    }
                    endpoint++;
                }
                // Current conservative bounds BEFORE firing; actual lifted palms are checked by the pair DURING playback.
                foreach (var body in new[] { farewellFirst, farewellSecond })
                {
                    var renderers = body.VisualRoot.GetComponentsInChildren<Renderer>();
                    if (ReadinessRefused(renderers.Length == 0 || renderers.Length > 64, "actual renderer count")) return false;
                    int visibleRenderers = 0;
                    foreach (var renderer in renderers)
                    {
                        if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                        visibleRenderers++;
                        var bounds = renderer.bounds;
                        for (int i = 0; i < 8; i++)
                            if (!Point(bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1,
                                (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))))
                            { ViewportRefused("actor=" + body.CharacterId + " renderer=" + FarewellPair.DiagnosticId(renderer) + " corner=" + i, rejectedViewport, camera); return false; }
                    }
                    if (ReadinessRefused(visibleRenderers == 0, "no enabled renderer")) return false;
                }
                return true;
            }

            // No diagnostic performs CanBegin/Read/Owns/arrival/camera predicates again or drives a body.
            private bool ReadinessRefused(bool refused, string reason)
            {
                if (!refused) return false;
                farewellLastRefusal = reason;
                if (farewellFirstRefusal == null) farewellFirstRefusal = ReadinessSnapshot(reason, 2048);
                return true;
            }

            private void ViewportRefused(string label, Vector3 actual, Camera camera)
            {
                if (farewellFirstViewportRefusal == null)
                    farewellFirstViewportRefusal = FarewellPair.DiagnosticText("frame=" + Time.frameCount + " " + label
                        + " actualViewport=" + FarewellPair.DiagnosticVector(actual) + " camera=" + FarewellPair.DiagnosticId(camera)
                        + " near=" + FarewellPair.DiagnosticFloat(camera.nearClipPlane) + " far=" + FarewellPair.DiagnosticFloat(camera.farClipPlane), 384);
                ReadinessRefused(true, "actual viewport rejected");
            }

            private string ReadinessSnapshot(string reason, int cap)
            {
                var report = new System.Text.StringBuilder(1024);
                try
                {
                    var rig = director.cameraRig; var camera = rig != null ? rig.ViewCamera : null;
                    report.Append("frame=").Append(Time.frameCount).Append(" delta=").Append(FarewellPair.DiagnosticFloat(Time.deltaTime))
                        .Append(" reason=").Append(reason).Append(" lastRefusal=").Append(farewellLastRefusal)
                        .Append(" capturedLoad=").Append(farewellLoad).Append(" currentLoad=").Append(director.loadGeneration)
                        .Append(" capturedOwnerSame=").Append(ReferenceEquals(director.npcMeetings, farewellOwner))
                        .Append(" generation=").Append(FarewellPair.DiagnosticText(farewellGeneration, 64))
                        .Append("/").Append(FarewellPair.DiagnosticText(director.npcMeetings != null ? director.npcMeetings.Generation : null, 64))
                        .Append(" readyFrames=").Append(farewellReadyFrames).Append(" cutFrame=").Append(farewellCutFrame);
                    void Endpoint(string label, CharacterPresentation body, HouseNpcMotion motion,
                        CharacterPresentation.FarewellBodyIdentity captured, string token)
                    {
                        var seat = body != null ? body.GetComponent<HouseSeatPresentation>() : null;
                        report.Append(" ").Append(label).Append("{actor=").Append(FarewellPair.DiagnosticText(body != null ? body.CharacterId : null, 64))
                            .Append(" lease=").Append(FarewellPair.DiagnosticText(token, 64)).Append("/").Append(FarewellPair.DiagnosticText(motion != null ? motion.LeaseId : null, 64))
                            .Append(" captured=").Append(FarewellPair.DiagnosticId(captured.Animator)).Append("/").Append(FarewellPair.DiagnosticId(captured.Controller))
                            .Append("/").Append(FarewellPair.DiagnosticId(captured.Avatar)).Append(" revision=").Append(captured.RequestRevision)
                            .Append(" seat=").Append(seat != null && seat.Active).Append(" parked=").Append(motion != null && motion.IsParked)
                            .Append(" motionState=").Append(motion != null ? motion.State.ToString() : "null")
                            .Append(" reserved=").Append(FarewellPair.DiagnosticVector(motion != null ? motion.ReservedDestination : default))
                            .Append("}");
                    }
                    report.Append(" camera{held=").Append(holdsCamera).Append(" capturedRig=").Append(FarewellPair.DiagnosticId(farewellRig))
                        .Append(" currentRig=").Append(FarewellPair.DiagnosticId(rig)).Append(" captured=").Append(FarewellPair.DiagnosticId(farewellCamera))
                        .Append(" current=").Append(FarewellPair.DiagnosticId(camera)).Append(" travelling=").Append(rig != null && rig.IsTravelling);
                    if (camera != null)
                        report.Append(" actualEye=").Append(FarewellPair.DiagnosticVector(camera.transform.position))
                            .Append(" actualFov=").Append(FarewellPair.DiagnosticFloat(camera.fieldOfView))
                            .Append(" requestedFov=").Append(FarewellPair.DiagnosticFloat(Lens))
                            .Append(" actualRotation=").Append(camera.transform.rotation.ToString("R"))
                            .Append(" actualAspect=").Append(FarewellPair.DiagnosticFloat(camera.aspect));
                    report.Append(" shotFocus=").Append(FarewellPair.DiagnosticVector(farewellShot.Focus))
                        .Append(" shotPitch=").Append(FarewellPair.DiagnosticFloat(farewellShot.Pitch)).Append(" shotYaw=").Append(FarewellPair.DiagnosticFloat(farewellShot.Yaw))
                        .Append(" shotDistance=").Append(FarewellPair.DiagnosticFloat(farewellShot.Distance))
                        .Append(" appliedDistance=").Append(FarewellPair.DiagnosticFloat(rig != null ? rig.AppliedDistance : 0f))
                        .Append("} firstViewport{").Append(farewellFirstViewportRefusal).Append("}");
                    Endpoint("first", farewellFirst, farewellMotionA, farewellBodyA, farewellTokenA);
                    Endpoint("second", farewellSecond, farewellMotionB, farewellBodyB, farewellTokenB);
                    report.Append(" firstNative{").Append(FarewellPair.DiagnosticNative(farewellFirst, 512))
                        .Append("} secondNative{").Append(FarewellPair.DiagnosticNative(farewellSecond, 512)).Append("}");
                }
                catch (System.Exception error) { report.Append(" diagnostic unavailable: ").Append(error.GetType().Name); }
                return FarewellPair.DiagnosticText(report.ToString(), cap);
            }

            private void RestoreFarewellFace()
            {
                if (farewellFaceRestored || !farewellFramed || !Active || !holdsCamera || Step != CeremonyStageStep.Goodbye
                    || !ReferenceEquals(director.ceremonyStage, this) || director.loadGeneration != farewellLoad
                    || director.departingId != leavingId || director.cameraRig != farewellRig
                    || farewellRig == null || farewellRig.ViewCamera != farewellCamera) return;
                farewellFaceRestored = true;
                var body = director.BodyFor(leavingId);
                if (body != null) Cut(new HouseCameraRig.Shot { Focus = body.position + Vector3.up * StandingFace,
                    Distance = 2.4f, Pitch = 10f, Yaw = body.eulerAngles.y + 180f, FieldOfView = Lens, DepthOfFieldWeight = .5f });
            }

            public void EndTheGoodbyeFarewell(string reason)
            {
                farewellAtGoodbye = false;
                bool complete = director.goodbyeFarewell != null && director.goodbyeFarewell.Stage == FarewellPair.Phase.Complete;
                if (!complete && !farewellPrimitive && farewellOwner != null && farewellTerminalRefusal == null)
                    farewellTerminalRefusal = ReadinessSnapshot(reason, 2048); // BEFORE Cancel/release/camera restoration.
                if (director.goodbyeFarewell != null)
                {
                    director.goodbyeFarewell.Cancel(reason);
                    UnityEngine.Object.Destroy(director.goodbyeFarewell.gameObject);
                    director.goodbyeFarewell = null;
                }
                if (farewellOwner == null) return;
                if (!complete && !farewellPrimitive) farewellFailure = farewellFailure ?? reason;
                // Exact captured tokens/places/generation ONLY, before departure or stage ownership is released.
                if (farewellOwner.Generation == farewellGeneration)
                {
                    if (OwnsFarewellLease(farewellFirst, farewellMotionA, farewellPlaceA, leavingId, farewellTokenA))
                        farewellOwner.ReleaseCeremonyActor(leavingId);
                    if (OwnsFarewellLease(farewellSecond, farewellMotionB, farewellPlaceB, survivorId, farewellTokenB))
                        farewellOwner.ReleaseCeremonyActor(survivorId);
                }
                RestoreFarewellFace();
                farewellOwner = null;
            }

            private bool OwnsFarewellLease(CharacterPresentation body, HouseNpcMotion motion, HouseInteractionAnchor place, string id, string token)
                => body != null && body.CharacterId == id && Visual(id) == body && motion != null
                    && body.GetComponent<HouseNpcMotion>() == motion && motion.IsBound && motion.BoundNpcId == id
                    && motion.LeaseId == token && farewellOwner.CeremonyActorHolds(id) && farewellOwner.CeremonyPlace(id) == place;
        }
    }
}
