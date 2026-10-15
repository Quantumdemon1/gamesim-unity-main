using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>Actual typed playback/landmarks, never LastReaction or a simulated pose.</summary>
    public sealed partial class CharacterPresentation
    {
        public const string FarewellState = "ReactFarewell", FarewellTake = "React_farewell";
        public const float FarewellNeutral = 29f / 30f;
        private static readonly int FarewellStateHash = Animator.StringToHash(FarewellState);
        private readonly List<AnimatorClipInfo> farewellClips = new List<AnimatorClipInfo>(4);
        private readonly List<AnimatorClipInfo> farewellIdleClips = new List<AnimatorClipInfo>(4);
        internal readonly struct FarewellBodyIdentity
        {
            internal readonly Transform Visual;
            internal readonly GameObject ProvidedRoot;
            internal readonly Animator Animator;
            internal readonly RuntimeAnimatorController Controller;
            internal readonly Avatar Avatar;
            internal readonly CharacterBodyBuildState Build;
            internal readonly int RequestRevision;
            internal FarewellBodyIdentity(Transform visual, GameObject root, Animator live, CharacterBodyBuildState build)
            { Visual = visual; ProvidedRoot = root; Animator = live; Controller = live.runtimeAnimatorController;
                Avatar = live.avatar; Build = build; RequestRevision = build != null ? build.Revision : 0; }
        }

        internal bool TryGetFarewellBody(out FarewellBodyIdentity identity)
        {
            identity = default;
            if (!built || !providedBody.Exists || IsBodyAssembling || IsChangingOutfit || visual == null) return false;
            var live = providedBody.Root.GetComponentInChildren<Animator>(true);
            var buildState = providedBody.Root.GetComponent<CharacterBodyBuildState>();
            if (live == null || live != animator || !live.isActiveAndEnabled || !live.isInitialized
                || !live.isHuman || live.avatar == null || !live.avatar.isValid || live.runtimeAnimatorController == null
                || live.applyRootMotion || live.speed != 1f || live.updateMode != AnimatorUpdateMode.Normal
                || buildState != null && !buildState.Ready || providedBody.Deferred && buildState == null) return false;
            identity = new FarewellBodyIdentity(visual, providedBody.Root, live, buildState);
            return true;
        }

        internal bool HoldsFarewellBody(FarewellBodyIdentity captured) => TryGetFarewellBody(out var live)
            && live.Visual == captured.Visual && live.ProvidedRoot == captured.ProvidedRoot && live.Animator == captured.Animator
            && live.Controller == captured.Controller && live.Avatar == captured.Avatar && live.Build == captured.Build
            && live.RequestRevision == captured.RequestRevision;

        internal bool CanBeginFarewell => !reducedMotion && !seated && pendingReaction < 0 && !talking && !arguing
            && activity == BodyActivity.None && !IsReacting && IsStill
            && TryReadFarewell(out var pose) && pose.Idle && !ForeignFarewellCue((int)Reaction.Farewell);

        /// <summary>Observed zero-contribution graph boundaries, never proof of played take or settled Idle.</summary>
        public enum FarewellBoundary { None, IncomingZeroWeight, OutgoingZeroWeight }

        public readonly struct FarewellObservation
        {
            public readonly bool InTake, Idle;
            public readonly FarewellBoundary Boundary;
            public readonly float NormalizedTime, SelectedClipWeight;
            public readonly Vector3 Root, Head, LeftHand, RightHand, LeftFoot, RightFoot, LeftToes, RightToes;
            public readonly Quaternion Heading;
            internal FarewellObservation(bool inTake, bool idle, FarewellBoundary boundary, float time, float weight, Transform root,
                Transform head, Transform leftHand, Transform rightHand, Transform leftFoot,
                Transform rightFoot, Transform leftToes, Transform rightToes)
            {
                InTake = inTake; Idle = idle; Boundary = boundary; NormalizedTime = time; SelectedClipWeight = weight;
                Root = root.position; Heading = root.rotation; Head = head.position;
                LeftHand = leftHand.position; RightHand = rightHand.position;
                LeftFoot = leftFoot.position; RightFoot = rightFoot.position;
                LeftToes = leftToes.position; RightToes = rightToes.position;
            }
        }

        public bool HasFarewellTake
        {
            get
            {
                if (!TryGetFarewellBody(out _) || !animator.HasState(0, FarewellStateHash)
                    || !animator.HasState(0, Animator.StringToHash("Idle"))
                    || !Supports(Reaction.Farewell)) return false;
                return TryGetFarewellMotion(out _);
            }
        }

        private bool TryGetFarewellMotion(out AnimationClip motion)
        {
                // Pre-trigger support trusts the generated graph's native Edit proof, not animationClips/HasState
                // as state-to-motion introspection. Actual selected clip identity is checked below DURING entry/playback.
                motion = null;
                // The generator's exact base graph, carried by the same handle UmaBodyProvider unwraps.
                // Its only production overrides are idle/run, never this newly authored beat.
                var handle = Resources.Load<AnimatorOverrideController>("Animation/GamesimHumanoid");
                var authority = handle != null ? handle.runtimeAnimatorController : null;
                var live = animator != null ? animator.runtimeAnimatorController : null;
                var overrides = live as AnimatorOverrideController;
                if (authority == null || live == null || (live != authority && (overrides == null
                    || overrides.runtimeAnimatorController != authority || overrides.overridesCount > 128))) return false;
                int matches = 0;
                var clips = authority.animationClips;
                if (clips == null || clips.Length > 128) return false;
                foreach (var clip in clips)
                {
                    if (clip == null || clip.name != FarewellTake) continue;
                    if (!clip.humanMotion || clip.isLooping || !FiniteFarewell(clip.length)
                        || Mathf.Abs(clip.length - 1f) > .001f) return false;
                    motion = clip; matches++;
                }
                if (matches != 1) { motion = null; return false; }
                var selected = overrides != null ? overrides[motion] : motion;
                return selected == null || selected == motion; // Null override means the exact base motion.
        }

        // This seam is ONLY authorized by the current two-principal ceremony transaction.
        // The runtime has no state-to-motion introspector: native Edit proof binds the generated
        // Idle/WalkStop states, and every read below also requires the actual selected clip object.
        internal readonly struct FarewellIdlePreparation
        {
            internal readonly FarewellBodyIdentity Body;
            internal readonly AnimationClip SourceMotion, IdleMotion, StopMotion;
            internal readonly bool WasIdle;
            internal readonly int Frame, StateHash;
            internal readonly float Phase;
            internal FarewellIdlePreparation(FarewellBodyIdentity body, AnimationClip source,
                AnimationClip idle, AnimationClip stop, bool wasIdle, AnimatorStateInfo state)
            {
                Body = body; SourceMotion = source; IdleMotion = idle; StopMotion = stop; WasIdle = wasIdle;
                Frame = Time.frameCount; StateHash = state.fullPathHash; Phase = state.normalizedTime;
            }
        }

        private readonly List<KeyValuePair<AnimationClip, AnimationClip>> farewellIdleOverrides =
            new List<KeyValuePair<AnimationClip, AnimationClip>>(128);

        private bool CanOwnFarewellIdle(FarewellBodyIdentity captured)
            => this.isActiveAndEnabled && HoldsFarewellBody(captured) && HasFarewellTake && !reducedMotion && !seated
                && pendingReaction < 0 && !talking && !arguing && activity == BodyActivity.None
                && !IsReacting && IsStill && !ForeignFarewellCue((int)Reaction.Farewell)
                && !animator.GetBool(ReactionParams[(int)Reaction.Farewell])
                && FiniteFarewell(transform.position) && FiniteFarewell(transform.rotation);

        private bool TryGetFarewellIdleAuthority(out AnimationClip idle, out AnimationClip stop)
        {
            idle = stop = null;
            var handle = Resources.Load<AnimatorOverrideController>("Animation/GamesimHumanoid");
            var authority = handle != null ? handle.runtimeAnimatorController : null;
            var live = animator != null ? animator.runtimeAnimatorController : null;
            var overrides = live as AnimatorOverrideController;
            if (authority == null || live == null || authority is AnimatorOverrideController
                || live != authority && (overrides == null || overrides.runtimeAnimatorController != authority
                    || overrides.overridesCount < 1 || overrides.overridesCount > 128)) return false;
            var clips = authority.animationClips;
            if (clips == null || clips.Length == 0 || clips.Length > 128) return false;
            AnimationClip Base(string name)
            {
                AnimationClip found = null;
                foreach (var clip in clips)
                {
                    if (clip == null) return null;
                    if (clip.name != name) continue;
                    if (found != null) return null;
                    found = clip;
                }
                return found;
            }
            var baseIdle = Base("Idle_Loop"); var baseRun = Base("Jog_Fwd_Loop"); stop = Base("WalkStop");
            if (baseIdle == null || baseRun == null || stop == null || !stop.humanMotion || stop.isLooping
                || !FiniteFarewell(stop.length) || stop.length <= 0f) return false;
            idle = baseIdle;
            if (overrides != null)
            {
                farewellIdleOverrides.Clear();
                overrides.GetOverrides(farewellIdleOverrides);
                if (farewellIdleOverrides.Count != overrides.overridesCount || farewellIdleOverrides.Count > 128) return false;
                var seen = new HashSet<AnimationClip>();
                foreach (var row in farewellIdleOverrides)
                {
                    if (row.Key == null || !seen.Add(row.Key) || System.Array.IndexOf(clips, row.Key) < 0) return false;
                    if (row.Value == null || row.Value == row.Key) continue;
                    // EXACT existing UmaBodyProvider exceptions, never an arbitrary controller/name.
                    if (row.Key != baseIdle && row.Key != baseRun || !row.Value.humanMotion
                        || !FiniteFarewell(row.Value.length) || row.Value.length <= 0f) return false;
                    if (row.Key == baseIdle) idle = row.Value;
                }
            }
            return idle != null && idle.humanMotion && idle.isLooping && FiniteFarewell(idle.length)
                && idle.length > 0f && idle != stop;
        }

        private bool FarewellSelectedMotion(bool next, AnimationClip expected, bool positive)
        {
            farewellIdleClips.Clear();
            if (next) animator.GetNextAnimatorClipInfo(0, farewellIdleClips);
            else animator.GetCurrentAnimatorClipInfo(0, farewellIdleClips);
            if (farewellIdleClips.Count != 1 || farewellIdleClips[0].clip != expected) return false;
            float weight = farewellIdleClips[0].weight;
            return FiniteFarewell(weight) && weight >= 0f && weight <= 1f && (!positive || weight > 0f);
        }

        internal bool TryCaptureFarewellIdle(FarewellBodyIdentity captured, out FarewellIdlePreparation record)
        {
            record = default;
            if (!CanOwnFarewellIdle(captured) || animator.IsInTransition(0)
                || !TryGetFarewellIdleAuthority(out var idle, out var stop)) return false;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            bool isIdle = state.IsName("Idle");
            if (!isIdle && !state.IsName("WalkStop") || !FiniteFarewell(state.normalizedTime)
                || state.normalizedTime < 0f || !animator.HasState(0, IdleStateHash)) return false;
            var selected = isIdle ? idle : stop;
            if (!FarewellSelectedMotion(false, selected, true)) return false;
            record = new FarewellIdlePreparation(captured, selected, idle, stop, isIdle, state);
            return true;
        }

        internal bool HoldsFarewellIdlePreparation(FarewellIdlePreparation record)
            => record.Frame == Time.frameCount && TryCaptureFarewellIdle(record.Body, out var now)
                && now.WasIdle == record.WasIdle && now.StateHash == record.StateHash
                && now.SourceMotion == record.SourceMotion && now.IdleMotion == record.IdleMotion
                && now.StopMotion == record.StopMotion;

        // No generic cancel/trigger reset or rollback is appropriate for this no-cue preparation.
        // A newer state/cue/body must never be crossfaded by the stale ceremony.
        internal bool HoldsFarewellIdleAfterPreparation(FarewellIdlePreparation record)
        {
            if (!CanOwnFarewellIdle(record.Body) || !TryGetFarewellIdleAuthority(out var idle, out var stop)
                || idle != record.IdleMotion || stop != record.StopMotion) return false;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            bool transition = animator.IsInTransition(0);
            if (!transition)
                return state.IsName("Idle") && FarewellSelectedMotion(false, idle, true)
                    || !record.WasIdle && state.IsName("WalkStop") && FarewellSelectedMotion(false, stop, true);
            var next = animator.GetNextAnimatorStateInfo(0);
            return !record.WasIdle && state.IsName("WalkStop") && next.IsName("Idle")
                && FarewellSelectedMotion(false, stop, false) && FarewellSelectedMotion(true, idle, false);
        }

        internal bool PrepareCapturedFarewellIdle(FarewellIdlePreparation record)
        {
            if (!HoldsFarewellIdlePreparation(record)) return false;
            if (!record.WasIdle) animator.CrossFadeInFixedTime(IdleStateHash, .25f, 0);
            return true; // Idle is a NO-OP. The caller stamps one attempt BEFORE the first engine write.
        }

        /// <summary>Release ONLY this captured Farewell cue; never cancel a newer presentation owner.</summary>
        internal bool CancelOwnedFarewell(FarewellBodyIdentity captured)
        {
            if (!HoldsFarewellBody(captured) || !HasFarewellTake) return false;
            RefreshAnimatorParameters();
            int own = (int)Reaction.Farewell, trigger = ReactionParams[own];
            bool ownPending = pendingReaction == own;
            bool ownTrigger = animator.GetBool(trigger);
            var state = animator.GetCurrentAnimatorStateInfo(0);
            bool transition = animator.IsInTransition(0);
            var next = transition ? animator.GetNextAnimatorStateInfo(0) : default;
            bool foreignState = !state.IsName(FarewellState) && !state.IsName("Idle")
                || transition && !next.IsName(FarewellState) && !next.IsName("Idle");
            bool foreign = ForeignFarewellCue(own) || foreignState || seated || activity != BodyActivity.None
                || talking || arguing || !IsStill;
            bool readable = TryReadFarewell(out var observation);
            bool actualOwn = readable && observation.InTake;
            bool ownedBoundary = readable && observation.Boundary != FarewellBoundary.None;
            // Resetting OUR trigger never resets a newer reaction/gesture or its tracking fields.
            animator.ResetTrigger(trigger);
            if (ownPending) pendingReaction = -1;
            if (!foreign && (ownPending || ownTrigger || actualOwn || ownedBoundary))
            {
                pendingAge = 0f;
                reactionFromStill = false;
                giveWayHeld = 0f;
                // The consumed captured cue can still own an exact zero-weight transition.
                // This cleanup does not turn that boundary into playback/Idle evidence.
                if ((actualOwn || ownedBoundary) && animator.HasState(0, IdleStateHash)) animator.CrossFadeInFixedTime(IdleStateHash, .2f, 0);
            }
            return ownPending || ownTrigger || actualOwn || ownedBoundary;
        }

        private bool ForeignFarewellCue(int own)
        {
            if (pendingReaction >= 0 && pendingReaction != own) return true;
            for (int i = 0; i < ReactionParams.Length; i++)
                if (i != own && (reactionParams & (1 << i)) != 0 && animator.GetBool(ReactionParams[i])) return true;
            for (int i = 0; i < GestureParams.Length; i++)
                if ((gestureParams & (1 << i)) != 0 && animator.GetBool(GestureParams[i])) return true;
            return seatedGesturePending || hasSeatedClapParam && animator.GetBool(SeatedClapParam)
                || hasSeatedVictoryParam && animator.GetBool(SeatedVictoryParam);
        }

        public bool TryReadFarewell(out FarewellObservation observation)
        {
            observation = default;
            if (!HasFarewellTake || !FiniteFarewell(transform.position) || !FiniteFarewell(transform.rotation)) return false;
            var headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            var leftHandBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var rightHandBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var leftFootBone = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rightFootBone = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            var leftToesBone = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            var rightToesBone = animator.GetBoneTransform(HumanBodyBones.RightToes);
            foreach (var bone in new[] { headBone, leftHandBone, rightHandBone, leftFootBone, rightFootBone, leftToesBone, rightToesBone })
                if (bone == null || !FiniteFarewell(bone.position)) return false;
            var current = animator.GetCurrentAnimatorStateInfo(0);
            bool transition = animator.IsInTransition(0);
            var next = transition ? animator.GetNextAnimatorStateInfo(0) : default;
            bool currentTake = current.IsName(FarewellState), nextTake = transition && next.IsName(FarewellState);
            bool currentIdle = current.IsName("Idle"), nextIdle = transition && next.IsName("Idle");
            bool selectedTake = currentTake || nextTake;
            bool inTake = false, idle = false;
            var boundary = FarewellBoundary.None;
            float progress = currentTake ? current.normalizedTime : nextTake ? next.normalizedTime : 0f;
            float selectedWeight = 0f;
            if (selectedTake)
            {
                if (!FiniteFarewell(progress) || progress < 0f || progress > 2f) return false;
                // Only the exact Idle/Farewell crossing is lawful; foreign/self transitions are not completion.
                if (transition && !(currentIdle && nextTake || currentTake && nextIdle)) return false;
                farewellClips.Clear();
                if (currentTake) animator.GetCurrentAnimatorClipInfo(0, farewellClips);
                else animator.GetNextAnimatorClipInfo(0, farewellClips);
                if (farewellClips.Count != 1 || !TryGetFarewellMotion(out var selected)
                    || farewellClips[0].clip != selected) return false;
                selectedWeight = farewellClips[0].weight;
                if (!FiniteFarewell(selectedWeight) || selectedWeight < 0f || selectedWeight > 1f) return false;
                if (selectedWeight > 0f) inTake = true;
                else
                {
                    // Exactly zero is a typed graph observation, not an epsilon or an entered take.
                    if (!transition || ForeignFarewellCue((int)Reaction.Farewell)
                        || !TryReadFarewellIdleWeight(currentTake, out _)) return false;
                    if (currentIdle && nextTake) boundary = FarewellBoundary.IncomingZeroWeight;
                    else if (currentTake && nextIdle) boundary = FarewellBoundary.OutgoingZeroWeight;
                    else return false;
                }
            }
            else
            {
                if (transition || !currentIdle || !TryReadFarewellIdleWeight(false, out selectedWeight)) return false;
                idle = true; // A settled Idle also requires an actual positive selected clip.
            }
            observation = new FarewellObservation(inTake, idle, boundary, progress, selectedWeight,
                transform, headBone, leftHandBone, rightHandBone, leftFootBone, rightFootBone, leftToesBone, rightToesBone);
            return true;
        }

        private bool TryReadFarewellIdleWeight(bool next, out float weight)
        {
            weight = 0f;
            farewellIdleClips.Clear();
            if (next) animator.GetNextAnimatorClipInfo(0, farewellIdleClips);
            else animator.GetCurrentAnimatorClipInfo(0, farewellIdleClips);
            if (farewellIdleClips.Count != 1 || farewellIdleClips[0].clip == null) return false;
            weight = farewellIdleClips[0].weight;
            return FiniteFarewell(weight) && weight > 0f && weight <= 1f;
        }

        // ----- Planted feet ----------------------------------------------------------------------
        // The farewell is an upper-body beat on planted feet: FarewellPair refuses a foot that moves
        // more than FootTolerance (1 mm) from the cue's frame until the body is back in Idle. Neither
        // the take nor the cross-fade can keep that (measured 2026-10-15, every Idle frame against the
        // take): the take stands in PoseAtEase's stance, a foot 168 to 173 mm from where Idle_Loop puts
        // it on the mocap skeleton and 52 to 69 mm from UMA's Idle on a UMA body, at every phase of
        // either; and no Idle holds a foot to a millimetre - over its loop a foot moves up to 17 mm
        // (Idle_Loop) and 21 mm (UMA's Idle). So from the cue until the body has settled back in Idle,
        // the hips and the two leg chains keep the pose they had on the cue's frame, and only then ease
        // into the live Idle again. The upper body plays the take as authored, on the held hips.
        private const float FarewellHoldSettleSeconds = .1f, FarewellHoldReleaseSeconds = .3f,
            FarewellHoldGiveWaySeconds = .15f, FarewellHoldEntrySeconds = .5f;
        private Transform[] farewellHold;
        private Vector3[] farewellHoldPosition;
        private Quaternion[] farewellHoldRotation;
        private Animator farewellHoldAnimator;
        private float farewellHoldWeight, farewellHoldAge, farewellHoldSettled, farewellHoldRelease;
        private bool farewellHoldEntered;

        /// <summary>Whether this body is holding its hips and legs where the farewell's cue found them.</summary>
        internal bool HoldsFarewellStance => farewellHold != null;

        /// <summary>
        /// Takes hold of the hips and both leg chains as they stand, on the frame the farewell is cued.
        /// Only a body settled in Idle is held: a cue that lands on anything else plays as it always has.
        /// </summary>
        private void HoldFarewellStance()
        {
            // A cue repeated while its stance is still held keeps the stance it already has.
            if (farewellHold != null && farewellHoldRelease <= 0f && animator == farewellHoldAnimator) return;
            if (animator == null || !TryReadFarewell(out var pose) || !pose.Idle) return;
            EndFarewellStance();
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null) return;
            var chain = new List<Transform> { hips };
            foreach (var (toes, foot) in new[] { (HumanBodyBones.LeftToes, HumanBodyBones.LeftFoot),
                         (HumanBodyBones.RightToes, HumanBodyBones.RightFoot) })
            {
                var end = animator.GetBoneTransform(toes);
                if (end == null) end = animator.GetBoneTransform(foot);
                int at = chain.Count;
                for (var bone = end; bone != hips; bone = bone.parent)
                {
                    if (bone == null) return; // A leg that does not hang from the hips is not held.
                    chain.Insert(at, bone);
                }
            }
            farewellHold = chain.ToArray();
            farewellHoldPosition = new Vector3[farewellHold.Length];
            farewellHoldRotation = new Quaternion[farewellHold.Length];
            for (int i = 0; i < farewellHold.Length; i++)
            {
                farewellHoldPosition[i] = farewellHold[i].localPosition;
                farewellHoldRotation[i] = farewellHold[i].localRotation;
            }
            farewellHoldAnimator = animator;
            farewellHoldWeight = 1f;
            farewellHoldAge = farewellHoldSettled = farewellHoldRelease = 0f;
            farewellHoldEntered = false;
        }

        private void EndFarewellStance()
        {
            farewellHold = null; farewellHoldPosition = null; farewellHoldRotation = null; farewellHoldAnimator = null;
            farewellHoldWeight = 0f; farewellHoldEntered = false;
        }

        /// <summary>
        /// Run after the animator has written the frame. Holds the stance while the body is in Idle, the
        /// take, or the cross-fades between them; lets go once the body has stood in Idle again for
        /// <see cref="FarewellHoldSettleSeconds"/>, and sooner when something else takes the body: a walk,
        /// another beat, a seat, an activity, reduced motion, or a cue that never played.
        /// </summary>
        private void TickFarewellStance()
        {
            if (farewellHold == null) return;
            if (animator == null || animator != farewellHoldAnimator || !animator.isActiveAndEnabled || !animator.isInitialized)
            { EndFarewellStance(); return; } // A rebuilt or retired body: nothing of the old one to write.
            foreach (var bone in farewellHold) if (bone == null) { EndFarewellStance(); return; }
            float delta = Time.deltaTime;
            var current = animator.GetCurrentAnimatorStateInfo(0);
            bool transition = animator.IsInTransition(0);
            var next = transition ? animator.GetNextAnimatorStateInfo(0) : default;
            bool currentTake = current.IsName(FarewellState), nextTake = transition && next.IsName(FarewellState);
            bool currentIdle = current.IsName("Idle");
            bool lawful = (currentTake || currentIdle) && (!transition || nextTake || next.IsName("Idle"));
            farewellHoldEntered |= currentTake || nextTake;
            if (!lawful || reducedMotion || seated || activity != BodyActivity.None)
                farewellHoldRelease = Mathf.Max(farewellHoldRelease, 1f / FarewellHoldGiveWaySeconds);
            else if (farewellHoldEntered && currentIdle && !transition)
            {
                if ((farewellHoldSettled += delta) > FarewellHoldSettleSeconds)
                    farewellHoldRelease = Mathf.Max(farewellHoldRelease, 1f / FarewellHoldReleaseSeconds);
            }
            else if (!farewellHoldEntered && (farewellHoldAge += delta) > FarewellHoldEntrySeconds)
                farewellHoldRelease = Mathf.Max(farewellHoldRelease, 1f / FarewellHoldReleaseSeconds);
            if (farewellHoldRelease > 0f) farewellHoldWeight -= delta * farewellHoldRelease;
            if (farewellHoldWeight <= 0f) { EndFarewellStance(); return; }
            for (int i = 0; i < farewellHold.Length; i++)
            {
                var bone = farewellHold[i];
                if (farewellHoldWeight >= 1f)
                {
                    bone.localPosition = farewellHoldPosition[i];
                    bone.localRotation = farewellHoldRotation[i];
                    continue;
                }
                bone.localPosition = Vector3.Lerp(bone.localPosition, farewellHoldPosition[i], farewellHoldWeight);
                bone.localRotation = Quaternion.Slerp(bone.localRotation, farewellHoldRotation[i], farewellHoldWeight);
            }
        }

        internal static bool FiniteFarewell(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool FiniteFarewell(Vector3 value) => FiniteFarewell(value.x) && FiniteFarewell(value.y) && FiniteFarewell(value.z);
        internal static bool FiniteFarewell(Quaternion value) => FiniteFarewell(value.x) && FiniteFarewell(value.y) && FiniteFarewell(value.z) && FiniteFarewell(value.w);
    }
}
