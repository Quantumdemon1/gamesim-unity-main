#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Playables;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>Actual imported mocap playback. No ceremony, rendered-body, contact or UMA acceptance claim.</summary>
    public sealed class GroundedFarewellPlumbingPlayModeTests
    {
        private const string Skeleton = "Assets/Gamesim/Art/Authored/Animation/Humanoid/bb_anim_PoseAtEase.fbx";
        private GameObject first, second;
        private CharacterPresentation a, b;
        private SkeletonProvider provider;
        private AnimatorOverrideController unsupported;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            provider = new SkeletonProvider();
            CharacterBodySource.Register(provider);
            first = new GameObject("Farewell mocap first");
            second = new GameObject("Farewell mocap second");
            first.transform.position = new Vector3(1600f, 0f, 1600f);
            second.transform.position = first.transform.position + Vector3.right * 3.34f;
            a = CharacterPresentation.Attach(first, ContentCatalog.Create(91).Find(ContentCatalog.PlayerId), Color.white);
            b = CharacterPresentation.Attach(second, ContentCatalog.Create(92).contestants.First(p => !p.isPlayer), Color.white);
            a.SetReducedMotion(false); b.SetReducedMotion(false);
            float by = Time.realtimeSinceStartup + 3f;
            while ((!a.TryReadFarewell(out var left) || !left.Idle || !b.TryReadFarewell(out var right) || !right.Idle)
                && Time.realtimeSinceStartup < by) yield return null;
            Assert.That(a.TryReadFarewell(out var ar) && ar.Idle && b.TryReadFarewell(out var br) && br.Idle, Is.True,
                "Both genuine imported skeletons have the actual newly named take and initialized Idle.");
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (provider != null) CharacterBodySource.Unregister(provider);
            if (first != null) Object.Destroy(first);
            if (second != null) Object.Destroy(second);
            if (unsupported != null) Object.Destroy(unsupported);
            yield return null;
        }

        [UnityTest]
        [Category("FarewellCalibratedIkFeasibility")]
        public IEnumerator CalibratedIk_ProvidedSkeletonsFreezeTrainAndTestLocalHoldoutsBeforeStrictFarewell()
        {
            yield return GroundedFarewellCalibratedIkProbe.Observe(new[] { a, b }, "ordinary-provided-imported-skeleton", false);
        }

        [UnityTest]
        [Category("FarewellDynamicIkFeasibility")]
        public IEnumerator DynamicIk_ProvidedSkeletonsTestPerEvaluationMappingAndOwnedRelease()
        {
            yield return GroundedFarewellDynamicIkFeasibilityProbe.Observe(new[] { a, b }, "ordinary-provided-imported-skeleton", false);
        }

        [UnityTest]
        [Category("FarewellInertStreamDiagnostic")]
        public IEnumerator InertStream_ProvidedSkeletonsAttachAndRestoreOnlyTheirOwnedNode()
        {
            yield return GroundedFarewellInertStreamAdapter.Observe(new[] { a, b }, "ordinary-provided-imported-skeleton");
        }

        [UnityTest]
        [Category("FarewellStreamTopologyDiagnostic")]
        public IEnumerator StreamTopology_ProvidedSkeletonsObserveTheirExistingIdleGraph()
        {
            yield return GroundedFarewellStreamTopologyReader.Observe(new[] { a, b }, "ordinary-provided-imported-skeleton");
        }

        [UnityTest]
        [Category("FarewellPassiveIkDiagnostic")]
        public IEnumerator PassiveIk_ProvidedSkeletonsRecordGoalsAndLateIdlePose()
        {
            yield return GroundedFarewellPassiveIkObserver.Observe(new[] { a, b }, "ordinary-provided-imported-skeleton");
        }

        [UnityTest]
        [Category("FarewellIdleDiagnostic")]
        public IEnumerator UntouchedIdle_ProvidedSkeletonsRecordTwoSecondsWithoutFarewell()
        {
            yield return MeasureUntouchedIdle(new[] { a, b }, "ordinary-provided-imported-skeleton");
        }

        // Measurement only: leave the already selected Idle and its one baseline untouched.
        private static IEnumerator MeasureUntouchedIdle(CharacterPresentation[] bodies, string provenance)
        {
            const int maxSamples = 4096, maxText = 4 * 1024 * 1024;
            var rows = new System.Text.StringBuilder(4096);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var capture = typeof(CharacterPresentation).GetMethod("TryGetFarewellBody", flags);
            var holds = typeof(CharacterPresentation).GetMethod("HoldsFarewellBody", flags);
            var identities = new object[2]; var animators = new Animator[2]; var clips = new AnimationClip[2];
            var ids = new string[2]; var frames = new BodyFrame[2]; var culling = new AnimatorCullingMode[2];
            var hashes = new int[2]; var phases = new float[2]; var initialPhases = new float[2];
            var baseline = new CharacterPresentation.FarewellObservation[2];
            var observations = new int[2]; var maxFeet = new float[2, 4]; var maxRoot = new float[2]; var maxHeading = new float[2];
            int samples = 0, previousFrame = -1;
            float started = 0f, elapsed = 0f, timeScale = Time.timeScale;
            bool complete = false;
            rows.Append("IDLE-CONTROL version=1 provenance=").Append(provenance)
                .Append(" durationRequested=2 baselineReset=false animatorDriven=false footAcceptance=false\n");
            try
            {
                Assert.That(bodies.Length, Is.EqualTo(2)); Assert.That(capture, Is.Not.Null); Assert.That(holds, Is.Not.Null);
                Assert.That(IdleControlFinite(timeScale) && timeScale > 0f, Is.True);
                for (int i = 0; i < 2; i++)
                {
                    var body = bodies[i]; var args = new object[] { null };
                    Assert.That(body != null && !body.IsPrimitiveBody && !body.IsBodyAssembling && !body.IsChangingOutfit, Is.True);
                    Assert.That((bool)capture.Invoke(body, args), Is.True); identities[i] = args[0];
                    var identityType = identities[i].GetType();
                    var animatorField = identityType.GetField("Animator", flags); Assert.That(animatorField, Is.Not.Null);
                    animators[i] = (Animator)animatorField.GetValue(identities[i]);
                    ids[i] = body.CharacterId; frames[i] = body.Frame; culling[i] = animators[i].cullingMode;
                    Assert.That(body.TryReadFarewell(out baseline[i]) && baseline[i].Idle && !baseline[i].InTake
                        && baseline[i].Boundary == CharacterPresentation.FarewellBoundary.None, Is.True);
                    var state = animators[i].GetCurrentAnimatorStateInfo(0);
                    var selected = animators[i].GetCurrentAnimatorClipInfo(0);
                    Assert.That(!animators[i].IsInTransition(0) && state.IsName("Idle"), Is.True);
                    Assert.That(selected.Length, Is.EqualTo(1)); Assert.That(selected[0].clip, Is.Not.Null);
                    clips[i] = selected[0].clip; hashes[i] = state.fullPathHash; phases[i] = initialPhases[i] = state.normalizedTime;
                    Assert.That(IdleControlFinite(phases[i]) && phases[i] >= 0f, Is.True);
                    rows.Append("IDENTITY body=").Append(i).Append(" actor=").Append(ids[i]).Append(" frame=").Append(frames[i])
                        .Append(" baselineFrame=").Append(Time.frameCount).Append(" idleClip=").Append(clips[i].name)
                        .Append(" clipId=").Append(clips[i].GetEntityId().ToString()).Append(" clipLength=").Append(IdleControlNumber(clips[i].length))
                        .Append(" initialPhase=").Append(IdleControlNumber(initialPhases[i]));
                    foreach (string name in new[] { "Visual", "ProvidedRoot", "Animator", "Controller", "Avatar", "Build", "RequestRevision" })
                    {
                        var field = identityType.GetField(name, flags); Assert.That(field, Is.Not.Null);
                        var value = field.GetValue(identities[i]);
                        rows.Append(' ').Append(name).Append('=')
                            .Append(value is UnityEngine.Object live ? live.GetEntityId().ToString()
                                : value is int revision ? revision.ToString(System.Globalization.CultureInfo.InvariantCulture) : "0");
                    }
                    rows.Append(" speed=").Append(IdleControlNumber(animators[i].speed)).Append(" updateMode=").Append(animators[i].updateMode)
                        .Append(" cullingMode=").Append(culling[i]).Append(" applyRootMotion=").Append(animators[i].applyRootMotion).Append('\n');
                }
                started = Time.realtimeSinceStartup; // AFTER both actual identities and single baselines are captured.
                while (true)
                {
                    elapsed = Time.realtimeSinceStartup - started;
                    Assert.That(IdleControlFinite(elapsed) && elapsed >= 0f && Time.frameCount > previousFrame && Time.timeScale == timeScale, Is.True);
                    previousFrame = Time.frameCount;
                    for (int i = 0; i < 2; i++)
                    {
                        var body = bodies[i]; var animator = animators[i];
                        Assert.That((bool)holds.Invoke(body, new[] { identities[i] }) && body.CharacterId == ids[i]
                            && body.Frame == frames[i] && animator.cullingMode == culling[i], Is.True,
                            "Keep the captured visual/provided-root/Animator/controller/avatar/build/revision and actual body frame.");
                        Assert.That(body.TryReadFarewell(out var observed) && observed.Idle && !observed.InTake
                            && observed.Boundary == CharacterPresentation.FarewellBoundary.None, Is.True,
                            "Only settled Idle: no take, transition, or zero-weight boundary.");
                        var state = animator.GetCurrentAnimatorStateInfo(0); var selected = animator.GetCurrentAnimatorClipInfo(0);
                        Assert.That(!animator.IsInTransition(0) && state.IsName("Idle") && state.fullPathHash == hashes[i], Is.True);
                        Assert.That(selected.Length == 1 && selected[0].clip == clips[i], Is.True);
                        Assert.That(IdleControlFinite(selected[0].weight) && selected[0].weight > 0f && selected[0].weight <= 1f
                            && IdleControlFinite(state.normalizedTime) && state.normalizedTime >= phases[i], Is.True);
                        phases[i] = state.normalizedTime;
                        var feet = new[] { observed.LeftFoot, observed.RightFoot, observed.LeftToes, observed.RightToes };
                        var baseFeet = new[] { baseline[i].LeftFoot, baseline[i].RightFoot, baseline[i].LeftToes, baseline[i].RightToes };
                        var gaps = new float[4];
                        for (int foot = 0; foot < 4; foot++)
                        {
                            gaps[foot] = Vector3.Distance(baseFeet[foot], feet[foot]); Assert.That(IdleControlFinite(gaps[foot]), Is.True);
                        }
                        float rootGap = Vector3.Distance(baseline[i].Root, observed.Root), headingGap = Quaternion.Angle(baseline[i].Heading, observed.Heading);
                        Assert.That(IdleControlFinite(rootGap) && IdleControlFinite(headingGap), Is.True);
                        string row = "SAMPLE sample=" + samples + " body=" + i + " frame=" + Time.frameCount
                            + " elapsed=" + IdleControlNumber(elapsed) + " stateHash=" + state.fullPathHash
                            + " phase=" + IdleControlNumber(state.normalizedTime) + " weight=" + IdleControlNumber(selected[0].weight)
                            + " root=" + IdleControlVector(observed.Root) + " heading=" + IdleControlNumber(observed.Heading.x)
                            + "," + IdleControlNumber(observed.Heading.y) + "," + IdleControlNumber(observed.Heading.z) + "," + IdleControlNumber(observed.Heading.w)
                            + " rootGap=" + IdleControlNumber(rootGap) + " headingGap=" + IdleControlNumber(headingGap)
                            + " LeftFoot=" + IdleControlVector(feet[0]) + " LeftFootGap=" + IdleControlNumber(gaps[0])
                            + " RightFoot=" + IdleControlVector(feet[1]) + " RightFootGap=" + IdleControlNumber(gaps[1])
                            + " LeftToes=" + IdleControlVector(feet[2]) + " LeftToesGap=" + IdleControlNumber(gaps[2])
                            + " RightToes=" + IdleControlVector(feet[3]) + " RightToesGap=" + IdleControlNumber(gaps[3]) + "\n";
                        Assert.That(rows.Length + row.Length, Is.LessThanOrEqualTo(maxText - 4096)); rows.Append(row); observations[i]++;
                        for (int foot = 0; foot < 4; foot++) maxFeet[i, foot] = Mathf.Max(maxFeet[i, foot], gaps[foot]);
                        maxRoot[i] = Mathf.Max(maxRoot[i], rootGap); maxHeading[i] = Mathf.Max(maxHeading[i], headingGap);
                        Assert.That(rootGap, Is.LessThanOrEqualTo(.001f)); Assert.That(headingGap, Is.LessThanOrEqualTo(.05f));
                        // NO foot-tolerance pass assertion: observed Idle drift is the requested measurement.
                    }
                    samples++;
                    if (elapsed >= 2f)
                    {
                        Assert.That(samples, Is.GreaterThanOrEqualTo(2));
                        Assert.That(phases[0] > initialPhases[0] && phases[1] > initialPhases[1], Is.True,
                            "Both unchanged selected Idle clips genuinely advanced during the measured window.");
                        complete = true; break;
                    }
                    Assert.That(samples, Is.LessThan(maxSamples), "Finite diagnostic output bound, not a new product timing budget.");
                    yield return null;
                }
            }
            finally
            {
                for (int i = 0; i < 2; i++)
                {
                    if (observations[i] == 0) { rows.Append("UNMEASURED body=").Append(i).Append('\n'); continue; }
                    rows.Append("MAXIMUM body=").Append(i).Append(" observations=").Append(observations[i]).Append(" LeftFoot=").Append(IdleControlNumber(maxFeet[i, 0]))
                        .Append(" RightFoot=").Append(IdleControlNumber(maxFeet[i, 1])).Append(" LeftToes=").Append(IdleControlNumber(maxFeet[i, 2]))
                        .Append(" RightToes=").Append(IdleControlNumber(maxFeet[i, 3])).Append(" root=").Append(IdleControlNumber(maxRoot[i]))
                        .Append(" heading=").Append(IdleControlNumber(maxHeading[i])).Append('\n');
                }
                rows.Append("RESULT measurementComplete=").Append(complete).Append(" samples=").Append(samples)
                    .Append(" actualElapsed=").Append(IdleControlNumber(elapsed)).Append(" plantedAcceptance=false nativeAcceptance=false visualAcceptance=false\n");
                TestContext.WriteLine(rows.ToString()); // Partial numeric observations survive a truthful refusal.
            }
        }

        private static bool IdleControlFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static string IdleControlNumber(float value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        private static string IdleControlVector(Vector3 value) =>
            IdleControlNumber(value.x) + "," + IdleControlNumber(value.y) + "," + IdleControlNumber(value.z);

        [UnityTest]
        public IEnumerator TwoActualTakesEnterTogetherStayPlantedAndReturnToIdle()
        {
            Assert.That(a.TryReadFarewell(out var baseA), Is.True);
            Assert.That(b.TryReadFarewell(out var baseB), Is.True);
            int firstEntry = -1, secondEntry = -1, palms = 0;
            bool neutralA = false, neutralB = false, completed = false;
            a.React(CharacterPresentation.Reaction.Farewell); b.React(CharacterPresentation.Reaction.Farewell);
            float by = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < by)
            {
                yield return null;
                bool readA = a.TryReadFarewell(out var left), readB = b.TryReadFarewell(out var right);
                Assert.That(readA, Is.True, readA ? "" : FarewellNativeDiagnostic(a));
                Assert.That(readB, Is.True, readB ? "" : FarewellNativeDiagnostic(b));
                AssertObservedFarewell(a, left); AssertObservedFarewell(b, right);
                Planted(a, baseA, left); Planted(b, baseB, right);
                if (left.InTake && firstEntry < 0) firstEntry = Time.frameCount;
                if (right.InTake && secondEntry < 0) secondEntry = Time.frameCount;
                neutralA |= left.InTake && left.NormalizedTime >= CharacterPresentation.FarewellNeutral;
                neutralB |= right.InTake && right.NormalizedTime >= CharacterPresentation.FarewellNeutral;
                if (left.InTake && right.InTake && left.RightHand.y > baseA.RightHand.y + .03f && right.RightHand.y > baseB.RightHand.y + .03f) palms++;
                if (firstEntry >= 0 && secondEntry >= 0 && left.Idle && right.Idle) { completed = true; break; }
            }
            Assert.That(firstEntry, Is.GreaterThanOrEqualTo(0));
            Assert.That(secondEntry, Is.EqualTo(firstEntry));
            Assert.That(neutralA && neutralB && completed, Is.True, "Actual neutral29/30, then actual Idle; no elapsed-time completion surrogate.");
            Assert.That(palms, Is.GreaterThan(0));
            Assert.That(Object.FindObjectsByType<FarewellPair>(FindObjectsSortMode.None), Is.Empty,
                "Standalone noncontact playback does not fabricate a ceremony-qualified transaction.");
        }

        [UnityTest]
        public IEnumerator ADeclaredTriggerWithWrongMotionIsNotFarewellSupport()
        {
            var animator = a.VisualRoot.GetComponentInChildren<Animator>();
            var original = animator.runtimeAnimatorController;
            var capture = typeof(CharacterPresentation).GetMethod("TryGetFarewellBody", BindingFlags.Instance | BindingFlags.NonPublic);
            var held = new object[] { null };
            Assert.That((bool)capture.Invoke(a, held), Is.True);
            unsupported = new AnimatorOverrideController(original);
            unsupported[CharacterPresentation.FarewellTake] = original.animationClips.First(c => c.name == "bb_anim_PoseAtEase_loop");
            animator.runtimeAnimatorController = unsupported;
            yield return null;
            Assert.That(a.Supports(CharacterPresentation.Reaction.Farewell), Is.True, "The trigger declaration alone still exists.");
            Assert.That(a.HasFarewellTake, Is.False, "An override replacing the Farewell motion is refused before triggering.");
            Assert.That(a.TryReadFarewell(out _), Is.False);
            Assert.That((bool)typeof(CharacterPresentation).GetMethod("HoldsFarewellBody", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(a, new[] { held[0] }), Is.False, "The unchanged cached visual alone is not the captured controller/avatar identity.");
        }

        [UnityTest]
        public IEnumerator ReducedMotionRecordsNoActualFarewellEntry()
        {
            a.SetReducedMotion(true);
            a.React(CharacterPresentation.Reaction.Farewell);
            float by = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < by)
            {
                yield return null;
                Assert.That(a.TryReadFarewell(out var pose), Is.True);
                Assert.That(pose.InTake, Is.False, "LastReaction is not provided playback.");
            }
            Assert.That(a.LastReaction, Is.EqualTo(CharacterPresentation.Reaction.Farewell));
        }

        [UnityTest]
        public IEnumerator CancellingTheRealTakeDoesNotReplayItAtIdle()
        {
            var held = CaptureFarewell(a);
            var live = a.VisualRoot.GetComponentInChildren<Animator>();
            a.React(CharacterPresentation.Reaction.Farewell);
            Assert.That(live.GetBool(CharacterPresentation.FarewellState), Is.True, "The first control is the real still-pending public trigger.");
            CancelOwnFarewell(a, held);
            Assert.That(live.GetBool(CharacterPresentation.FarewellState), Is.False);
            yield return null;
            Assert.That(a.TryReadFarewell(out var pendingIdle) && pendingIdle.Idle && !pendingIdle.InTake, Is.True);

            a.React(CharacterPresentation.Reaction.Farewell);
            yield return WaitForFarewellSelection(a);
            Assert.That(a.TryReadFarewell(out var selected), Is.True);
            AssertObservedFarewell(a, selected);
            if (selected.Boundary != CharacterPresentation.FarewellBoundary.None)
            {
                // Cancel only a genuinely observed consumed native boundary. No Animator.Update/Play/time injection.
                CancelOwnFarewell(a, held);
                Assert.That(live.GetBool(CharacterPresentation.FarewellState), Is.False);
                float boundaryIdleBy = Time.realtimeSinceStartup + .5f;
                while ((!a.TryReadFarewell(out var boundaryIdle) || !boundaryIdle.Idle) && Time.realtimeSinceStartup < boundaryIdleBy) yield return null;
                bool boundarySettled = a.TryReadFarewell(out var settled) && settled.Idle && !settled.InTake;
                Assert.That(boundarySettled, Is.True, boundarySettled ? ""
                    : "A consumed-boundary cleanup returns to actual positive-clip Idle: " + FarewellNativeDiagnostic(a));
                // Preserve the original positive-entry cancellation/no-replay proof as a separate public cue.
                a.React(CharacterPresentation.Reaction.Farewell);
            }
            float entryBy = Time.realtimeSinceStartup + .5f;
            while ((!a.TryReadFarewell(out var entryPose) || !entryPose.InTake) && Time.realtimeSinceStartup < entryBy) yield return null;
            Assert.That(a.TryReadFarewell(out var entered) && entered.InTake, Is.True);
            CancelOwnFarewell(a, held);
            float idleBy = Time.realtimeSinceStartup + .5f;
            while ((!a.TryReadFarewell(out var idlePose) || !idlePose.Idle) && Time.realtimeSinceStartup < idleBy) yield return null;
            Assert.That(a.TryReadFarewell(out var idle) && idle.Idle, Is.True);
            float by = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < by)
            {
                yield return null;
                Assert.That(a.TryReadFarewell(out var now) && !now.InTake, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator OwnFarewellCleanupPreservesANewerPendingFuriousCue()
        {
            var held = CaptureFarewell(a);
            a.React(CharacterPresentation.Reaction.Farewell);
            a.React(CharacterPresentation.Reaction.Furious);
            var live = a.VisualRoot.GetComponentInChildren<Animator>();
            Assert.That(live.GetBool("ReactFurious"), Is.True);
            var bookkeeping = Bookkeeping(a);
            CancelOwnFarewell(a, held);
            Assert.That(Bookkeeping(a), Is.EqualTo(bookkeeping), "New pending reaction age/from-still/give-way ownership survives.");
            Assert.That(live.GetBool("ReactFurious"), Is.True, "Only the old Farewell trigger can be reset.");
            Assert.That(live.GetBool("ReactFarewell"), Is.False);
            Assert.That(a.LastReaction, Is.EqualTo(CharacterPresentation.Reaction.Furious));
            yield return WaitForActualState(live, "ReactFurious", .5f);
            Assert.That(live.GetCurrentAnimatorStateInfo(0).IsName("ReactFurious"), Is.True, "The surviving real trigger enters its actual state.");
            // Independent actual second body: retain the original pending-first-body assertions above.
            var heldB = CaptureFarewell(b);
            b.React(CharacterPresentation.Reaction.Farewell);
            yield return WaitForFarewellSelection(b);
            var otherLive = b.VisualRoot.GetComponentInChildren<Animator>();
            b.React(CharacterPresentation.Reaction.Furious);
            var otherCurrent = otherLive.GetCurrentAnimatorStateInfo(0).fullPathHash;
            bool otherTransition = otherLive.IsInTransition(0);
            int otherNext = otherTransition ? otherLive.GetNextAnimatorStateInfo(0).fullPathHash : 0;
            var otherBookkeeping = Bookkeeping(b);
            Assert.That(otherLive.GetBool("ReactFurious"), Is.True);
            AssertForeignZeroBoundaryRefused(b); // Raw native selection, not a post-new-cue reader-acceptance prerequisite.
            CancelOwnFarewell(b, heldB);
            Assert.That(otherLive.GetBool("ReactFurious"), Is.True, "The newer actual cue survives consumed-boundary/positive-take cleanup.");
            Assert.That(otherLive.GetCurrentAnimatorStateInfo(0).fullPathHash, Is.EqualTo(otherCurrent));
            Assert.That(otherLive.IsInTransition(0), Is.EqualTo(otherTransition), "Cleanup cannot introduce or replace a transition.");
            if (otherTransition) Assert.That(otherLive.GetNextAnimatorStateInfo(0).fullPathHash, Is.EqualTo(otherNext));
            Assert.That(Bookkeeping(b), Is.EqualTo(otherBookkeeping));
            Assert.That(b.LastReaction, Is.EqualTo(CharacterPresentation.Reaction.Furious));
            yield return WaitForActualState(otherLive, "ReactFurious", .5f);
        }

        [UnityTest]
        public IEnumerator OwnFarewellCleanupPreservesAnActuallyEnteredShockedState()
        {
            var held = CaptureFarewell(a);
            var live = a.VisualRoot.GetComponentInChildren<Animator>();
            a.React(CharacterPresentation.Reaction.Farewell);
            yield return WaitForActualState(live, CharacterPresentation.FarewellState, .5f);
            a.React(CharacterPresentation.Reaction.Shocked);
            yield return WaitForActualState(live, "ReactShocked", .5f);
            var before = live.GetCurrentAnimatorStateInfo(0);
            var bookkeeping = Bookkeeping(a);
            var clip = live.GetCurrentAnimatorClipInfo(0).Single().clip;
            CancelOwnFarewell(a, held);
            Assert.That(Bookkeeping(a), Is.EqualTo(bookkeeping));
            Assert.That(live.IsInTransition(0), Is.False, "A new current reaction is not cross-faded back to Idle.");
            Assert.That(live.GetCurrentAnimatorStateInfo(0).fullPathHash, Is.EqualTo(before.fullPathHash));
            Assert.That(live.GetCurrentAnimatorClipInfo(0).Single().clip, Is.SameAs(clip));
            Assert.That(a.LastReaction, Is.EqualTo(CharacterPresentation.Reaction.Shocked));
            yield return null;
            Assert.That(live.GetCurrentAnimatorStateInfo(0).IsName("ReactShocked"), Is.True);
            Assert.That(live.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThanOrEqualTo(before.normalizedTime));
        }

        [UnityTest]
        public IEnumerator OwnFarewellCleanupPreservesAnActualIncomingShockedTransition()
        {
            var held = CaptureFarewell(a);
            var live = a.VisualRoot.GetComponentInChildren<Animator>();
            a.React(CharacterPresentation.Reaction.Farewell);
            yield return WaitForActualState(live, CharacterPresentation.FarewellState, .5f);
            a.React(CharacterPresentation.Reaction.Shocked);
            float by = Time.realtimeSinceStartup + .5f;
            while ((!live.IsInTransition(0) || !live.GetNextAnimatorStateInfo(0).IsName("ReactShocked"))
                && Time.realtimeSinceStartup < by) yield return null;
            Assert.That(live.IsInTransition(0) && live.GetNextAnimatorStateInfo(0).IsName("ReactShocked"), Is.True,
                "A real incoming foreign transition is required, not a forced Animator state.");
            int current = live.GetCurrentAnimatorStateInfo(0).fullPathHash, next = live.GetNextAnimatorStateInfo(0).fullPathHash;
            Assert.That(a.TryReadFarewell(out _), Is.False, "A foreign incoming reaction is not either lawful Farewell/Idle boundary.");
            var bookkeeping = Bookkeeping(a);
            CancelOwnFarewell(a, held);
            Assert.That(Bookkeeping(a), Is.EqualTo(bookkeeping));
            Assert.That(live.IsInTransition(0), Is.True);
            Assert.That(live.GetCurrentAnimatorStateInfo(0).fullPathHash, Is.EqualTo(current));
            Assert.That(live.GetNextAnimatorStateInfo(0).fullPathHash, Is.EqualTo(next), "No replacement Idle transition steals the incoming reaction.");
            Assert.That(a.LastReaction, Is.EqualTo(CharacterPresentation.Reaction.Shocked));
            yield return WaitForActualState(live, "ReactShocked", .5f);
        }

        [UnityTest]
        public IEnumerator OwnFarewellCleanupPreservesANewerGestureTrigger()
        {
            var held = CaptureFarewell(a);
            a.React(CharacterPresentation.Reaction.Farewell);
            Assert.That(a.MakeGesture(CharacterPresentation.Gesture.Shrug), Is.True);
            var live = a.VisualRoot.GetComponentInChildren<Animator>();
            var beforeCurrent = live.GetCurrentAnimatorStateInfo(0).fullPathHash;
            var bookkeeping = Bookkeeping(a);
            Assert.That(live.GetBool("GestureShrug"), Is.True);
            CancelOwnFarewell(a, held);
            Assert.That(live.GetBool("GestureShrug"), Is.True, "A real queued gesture is not reset with the old reaction.");
            Assert.That(live.GetCurrentAnimatorStateInfo(0).fullPathHash, Is.EqualTo(beforeCurrent));
            Assert.That(live.IsInTransition(0), Is.False);
            Assert.That(a.LastGesture, Is.EqualTo(CharacterPresentation.Gesture.Shrug));
            Assert.That(Bookkeeping(a).Skip(1).ToArray(), Is.EqualTo(bookkeeping.Skip(1).ToArray()),
                "A new gesture's ownership does not permit resetting shared reaction timing flags.");
            yield return WaitForActualState(live, "Shrug", .5f);
            Assert.That(live.GetCurrentAnimatorStateInfo(0).IsName("Shrug"), Is.True);
            // Independent actual second body: retain the original pending-first-body assertions above.
            var heldB = CaptureFarewell(b);
            b.React(CharacterPresentation.Reaction.Farewell);
            yield return WaitForFarewellSelection(b);
            var otherLive = b.VisualRoot.GetComponentInChildren<Animator>();
            Assert.That(b.MakeGesture(CharacterPresentation.Gesture.Shrug), Is.True);
            var otherCurrent = otherLive.GetCurrentAnimatorStateInfo(0).fullPathHash;
            bool otherTransition = otherLive.IsInTransition(0);
            int otherNext = otherTransition ? otherLive.GetNextAnimatorStateInfo(0).fullPathHash : 0;
            var otherBookkeeping = Bookkeeping(b);
            Assert.That(otherLive.GetBool("GestureShrug"), Is.True);
            AssertForeignZeroBoundaryRefused(b); // Raw native selection, not a post-new-cue reader-acceptance prerequisite.
            CancelOwnFarewell(b, heldB);
            Assert.That(otherLive.GetBool("GestureShrug"), Is.True, "The newer actual cue survives consumed-boundary/positive-take cleanup.");
            Assert.That(otherLive.GetCurrentAnimatorStateInfo(0).fullPathHash, Is.EqualTo(otherCurrent));
            Assert.That(otherLive.IsInTransition(0), Is.EqualTo(otherTransition), "Cleanup cannot introduce or replace a transition.");
            if (otherTransition) Assert.That(otherLive.GetNextAnimatorStateInfo(0).fullPathHash, Is.EqualTo(otherNext));
            Assert.That(Bookkeeping(b).Skip(1).ToArray(), Is.EqualTo(otherBookkeeping.Skip(1).ToArray()));
            Assert.That(b.LastGesture, Is.EqualTo(CharacterPresentation.Gesture.Shrug));
            yield return WaitForActualState(otherLive, "Shrug", .5f);
        }

        [UnityTest]
        public IEnumerator OwnFarewellCleanupPreservesANewerCookingOwner()
        {
            var held = CaptureFarewell(a);
            var live = a.VisualRoot.GetComponentInChildren<Animator>();
            a.React(CharacterPresentation.Reaction.Farewell);
            yield return WaitForActualState(live, CharacterPresentation.FarewellState, .5f);
            a.SetActivity(CharacterPresentation.BodyActivity.Cooking);
            var bookkeeping = Bookkeeping(a);
            var before = live.GetCurrentAnimatorStateInfo(0).fullPathHash;
            CancelOwnFarewell(a, held);
            Assert.That(a.Activity, Is.EqualTo(CharacterPresentation.BodyActivity.Cooking));
            Assert.That(Bookkeeping(a), Is.EqualTo(bookkeeping), "Cleanup does not rewrite reaction tracking under a newer activity owner.");
            Assert.That(live.GetCurrentAnimatorStateInfo(0).fullPathHash, Is.EqualTo(before));
            Assert.That(live.IsInTransition(0), Is.False, "No cleanup-owned Idle transition masks the real activity transition.");
            yield return WaitForActualState(live, "Cook", .5f);
            Assert.That(live.GetBool("Cooking"), Is.True);
            Assert.That(a.Activity, Is.EqualTo(CharacterPresentation.BodyActivity.Cooking));
        }

        [UnityTest]
        public IEnumerator ArbitraryStandaloneBodiesCannotBorrowACeremony()
        {
            var method = typeof(FarewellPair).GetMethod("TryBegin", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            var result = method.Invoke(null, new object[] { a, b, "not-a-principal", null, null, null, null, null });
            Assert.That(result, Is.Null);
            yield return null;
            Assert.That(a.TryReadFarewell(out var left) && left.Idle && b.TryReadFarewell(out var right) && right.Idle, Is.True);
        }

        [TestCase("seated")]
        [TestCase("activity")]
        [TestCase("talking")]
        [TestCase("arguing")]
        public void LocalPreflightRefusesConflictingPresentationOwnership(string conflict)
        {
            // Explicit controlled negative flags, NOT production ceremony/seat evidence.
            Assert.That(CanBegin(a), Is.True);
            switch (conflict)
            {
                case "seated": a.SetSeated(true); break;
                case "activity": a.SetActivity(CharacterPresentation.BodyActivity.Cooking); break;
                case "talking": a.SetTalking(true); break;
                case "arguing": a.SetArguing(true); break;
                default: Assert.Fail("Unknown fixed test control."); break;
            }
            Assert.That(CanBegin(a), Is.False);
            Assert.That(a.LastReaction, Is.Not.EqualTo(CharacterPresentation.Reaction.Farewell));
        }

        [Test]
        public void AnotherActuallyPendingTriggerRefusesAtomicPreflight()
        {
            Assert.That(CanBegin(a), Is.True);
            a.React(CharacterPresentation.Reaction.Shocked);
            Assert.That(CanBegin(a), Is.False, "A pending real different trigger is not idle ownership.");
            Assert.That(a.LastReaction, Is.EqualTo(CharacterPresentation.Reaction.Shocked));
            Assert.That(b.LastReaction, Is.Not.EqualTo(CharacterPresentation.Reaction.Farewell));
        }

        private static void AssertForeignZeroBoundaryRefused(CharacterPresentation body)
        {
            var live = body.VisualRoot.GetComponentInChildren<Animator>();
            bool transition = live.IsInTransition(0);
            var current = live.GetCurrentAnimatorStateInfo(0);
            var next = transition ? live.GetNextAnimatorStateInfo(0) : default;
            bool incoming = transition && current.IsName("Idle") && next.IsName(CharacterPresentation.FarewellState);
            bool outgoing = transition && current.IsName(CharacterPresentation.FarewellState) && next.IsName("Idle");
            if (!incoming && !outgoing) return;
            var selected = incoming ? live.GetNextAnimatorClipInfo(0) : live.GetCurrentAnimatorClipInfo(0);
            var other = incoming ? live.GetCurrentAnimatorClipInfo(0) : live.GetNextAnimatorClipInfo(0);
            var handle = Resources.Load<AnimatorOverrideController>("Animation/GamesimHumanoid");
            Assert.That(handle, Is.Not.Null);
            var motion = handle.runtimeAnimatorController.animationClips.Single(c => c.name == CharacterPresentation.FarewellTake);
            bool zero = selected.Length == 1 && selected[0].clip == motion && selected[0].weight == 0f
                && other.Length == 1 && other[0].clip != null && !float.IsNaN(other[0].weight)
                && !float.IsInfinity(other[0].weight) && other[0].weight > 0f && other[0].weight <= 1f;
            Debug.Log("[Gamesim] actual newer-owner Farewell raw selection: current=" + current.fullPathHash
                + ", next=" + next.fullPathHash + ", selectedClips=" + selected.Length + ", selectedWeight="
                + (selected.Length == 1 ? selected[0].weight.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : "unavailable")
                + ", qualifiedZeroBoundary=" + zero);
            if (zero) Assert.That(body.TryReadFarewell(out _), Is.False,
                "A genuinely observed zero-weight boundary cannot be admitted after a newer actual reaction/gesture owns presentation.");
        }

        private static void AssertObservedFarewell(CharacterPresentation body, CharacterPresentation.FarewellObservation pose)
        {
            var live = body.VisualRoot.GetComponentInChildren<Animator>();
            Assert.That(live, Is.Not.Null);
            bool transition = live.IsInTransition(0);
            var current = live.GetCurrentAnimatorStateInfo(0);
            var next = transition ? live.GetNextAnimatorStateInfo(0) : default;
            if (pose.Boundary != CharacterPresentation.FarewellBoundary.None)
            {
                Assert.That(pose.InTake || pose.Idle, Is.False, "A zero-contribution boundary is neither playback nor settled Idle.");
                Assert.That(pose.SelectedClipWeight, Is.EqualTo(0f));
                Assert.That(transition, Is.True);
                bool incoming = pose.Boundary == CharacterPresentation.FarewellBoundary.IncomingZeroWeight;
                Assert.That(incoming
                    ? current.IsName("Idle") && next.IsName(CharacterPresentation.FarewellState)
                    : pose.Boundary == CharacterPresentation.FarewellBoundary.OutgoingZeroWeight
                        && current.IsName(CharacterPresentation.FarewellState) && next.IsName("Idle"), Is.True);
                var selected = incoming ? live.GetNextAnimatorClipInfo(0) : live.GetCurrentAnimatorClipInfo(0);
                var other = incoming ? live.GetCurrentAnimatorClipInfo(0) : live.GetNextAnimatorClipInfo(0);
                Assert.That(selected.Length, Is.EqualTo(1));
                var handle = Resources.Load<AnimatorOverrideController>("Animation/GamesimHumanoid");
                Assert.That(handle, Is.Not.Null);
                var sourceMotion = handle.runtimeAnimatorController.animationClips.Single(c => c.name == CharacterPresentation.FarewellTake);
                Assert.That(selected[0].clip, Is.SameAs(sourceMotion));
                Assert.That(selected[0].weight, Is.EqualTo(0f));
                Assert.That(other.Length, Is.EqualTo(1));
                Assert.That(other[0].clip, Is.Not.Null);
                Assert.That(other[0].weight, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
            }
            else if (pose.InTake)
            {
                Assert.That(pose.Idle, Is.False);
                Assert.That(pose.SelectedClipWeight, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
                var selected = current.IsName(CharacterPresentation.FarewellState)
                    ? live.GetCurrentAnimatorClipInfo(0) : live.GetNextAnimatorClipInfo(0);
                Assert.That(selected.Length, Is.EqualTo(1));
                Assert.That(selected[0].clip.name, Is.EqualTo(CharacterPresentation.FarewellTake));
                Assert.That(selected[0].weight, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
            }
            else
            {
                Assert.That(pose.Idle && !transition && current.IsName("Idle"), Is.True);
                var selected = live.GetCurrentAnimatorClipInfo(0);
                Assert.That(selected.Length, Is.EqualTo(1));
                Assert.That(selected[0].clip, Is.Not.Null);
                Assert.That(selected[0].weight, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
                Assert.That(pose.SelectedClipWeight, Is.EqualTo(selected[0].weight));
            }
        }

        private static string FarewellNativeDiagnostic(CharacterPresentation body)
        {
            var report = new System.Text.StringBuilder(512);
            string Float(float value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            string Name(Object value) => value == null ? "null" : value.name.Length <= 64 ? value.name : value.name.Substring(0, 64);
            try
            {
                report.Append("frame=").Append(Time.frameCount).Append(" delta=").Append(Float(Time.deltaTime));
                if (body == null) return report.Append(" body=null").ToString();
                report.Append(" id=").Append(body.CharacterId).Append(" frameKind=").Append(body.Frame)
                    .Append(" primitive=").Append(body.IsPrimitiveBody).Append(" building=").Append(body.IsBodyAssembling)
                    .Append(" changing=").Append(body.IsChangingOutfit).Append(" still=").Append(body.IsStill)
                    .Append(" seated=").Append(body.IsSeated).Append(" activity=").Append(body.Activity);
                var visual = body.VisualRoot;
                var live = visual != null ? visual.GetComponentInChildren<Animator>(true) : null;
                if (live == null) return report.Append(" animator=null").ToString();
                report.Append(" animator=").Append(live.GetEntityId().ToString()).Append(" enabled=").Append(live.isActiveAndEnabled)
                    .Append(" initialized=").Append(live.isInitialized).Append(" human=").Append(live.isHuman)
                    .Append(" controller=").Append(Name(live.runtimeAnimatorController)).Append("#")
                    .Append(live.runtimeAnimatorController != null ? live.runtimeAnimatorController.GetEntityId().ToString() : "0")
                    .Append(" avatar=").Append(Name(live.avatar)).Append("#").Append(live.avatar != null ? live.avatar.GetEntityId().ToString() : "0");
                if (live.isInitialized && live.runtimeAnimatorController != null)
                {
                    var current = live.GetCurrentAnimatorStateInfo(0);
                    bool transition = live.IsInTransition(0);
                    var next = transition ? live.GetNextAnimatorStateInfo(0) : default;
                    report.Append(" current=").Append(current.fullPathHash).Append("/").Append(Float(current.normalizedTime))
                        .Append(" currentIdle=").Append(current.IsName("Idle")).Append(" currentFarewell=").Append(current.IsName(CharacterPresentation.FarewellState))
                        .Append(" transition=").Append(transition).Append(" next=").Append(next.fullPathHash).Append("/").Append(Float(next.normalizedTime))
                        .Append(" nextIdle=").Append(next.IsName("Idle")).Append(" nextFarewell=").Append(next.IsName(CharacterPresentation.FarewellState));
                    void Clips(string label, AnimatorClipInfo[] rows)
                    {
                        report.Append(" ").Append(label).Append("Count=").Append(rows.Length);
                        for (int i = 0; i < rows.Length && i < 4; i++)
                            report.Append(" [").Append(Name(rows[i].clip)).Append("#")
                                .Append(rows[i].clip != null ? rows[i].clip.GetEntityId().ToString() : "0").Append(" weight=").Append(Float(rows[i].weight)).Append("]");
                        if (rows.Length > 4) report.Append(" [remaining clips omitted]");
                    }
                    Clips("currentClip", live.GetCurrentAnimatorClipInfo(0));
                    if (transition) Clips("nextClip", live.GetNextAnimatorClipInfo(0));
                }
            }
            catch (System.Exception error) { report.Append(" diagnostic unavailable: ").Append(error.GetType().Name); }
            return report.Length <= 2048 ? report.ToString() : report.ToString(0, 2048);
        }

        private static IEnumerator WaitForFarewellSelection(CharacterPresentation body)
        {
            float by = Time.realtimeSinceStartup + .5f;
            while (Time.realtimeSinceStartup < by)
            {
                if (body.TryReadFarewell(out var pose) && (pose.InTake || pose.Boundary != CharacterPresentation.FarewellBoundary.None))
                {
                    AssertObservedFarewell(body, pose);
                    Debug.Log("[Gamesim] actual Farewell cleanup selection: boundary=" + pose.Boundary + ", weight="
                        + pose.SelectedClipWeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                    yield break; // A source-observed prerequisite, not a skipped test or an inferred zero-weight sample.
                }
                yield return null;
            }
            Assert.Fail("Public Farewell did not produce a bounded actual selected cue: " + FarewellNativeDiagnostic(body));
        }

        private static bool CanBegin(CharacterPresentation body) => (bool)typeof(CharacterPresentation)
            .GetProperty("CanBeginFarewell", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(body);

        [Test]
        public void AnotherActuallyPendingGestureRefusesAtomicPreflightWithoutConsumingIt()
        {
            Assert.That(CanBegin(a), Is.True);
            Assert.That(a.MakeGesture(CharacterPresentation.Gesture.Shrug), Is.True);
            var live = a.VisualRoot.GetComponentInChildren<Animator>();
            var bookkeeping = Bookkeeping(a);
            Assert.That(live.GetBool("GestureShrug"), Is.True);
            Assert.That(CanBegin(a), Is.False, "Actual declared pending gesture ownership is not free Idle ownership.");
            Assert.That(live.GetBool("GestureShrug"), Is.True, "Read-only refusal does not consume the actual public cue.");
            Assert.That(Bookkeeping(a), Is.EqualTo(bookkeeping));
            Assert.That(a.LastGesture, Is.EqualTo(CharacterPresentation.Gesture.Shrug));
            Assert.That(a.LastReaction, Is.Not.EqualTo(CharacterPresentation.Reaction.Farewell));
            Assert.That(b.LastReaction, Is.Not.EqualTo(CharacterPresentation.Reaction.Farewell));
        }

        private static object CaptureFarewell(CharacterPresentation body)
        {
            var held = new object[] { null };
            var method = typeof(CharacterPresentation).GetMethod("TryGetFarewellBody", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            Assert.That((bool)method.Invoke(body, held), Is.True);
            return held[0];
        }

        private static void CancelOwnFarewell(CharacterPresentation body, object held)
        {
            var method = typeof(CharacterPresentation).GetMethod("CancelOwnedFarewell", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(body, new[] { held });
        }

        private static object[] Bookkeeping(CharacterPresentation body) => new[]
        {
            "pendingReaction", "pendingAge", "reactionFromStill", "giveWayHeld"
        }.Select(name => typeof(CharacterPresentation).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(body)).ToArray();

        private static IEnumerator WaitForActualState(Animator live, string state, float seconds)
        {
            float by = Time.realtimeSinceStartup + seconds;
            while ((live.IsInTransition(0) || !live.GetCurrentAnimatorStateInfo(0).IsName(state)) && Time.realtimeSinceStartup < by)
                yield return null;
            Assert.That(!live.IsInTransition(0) && live.GetCurrentAnimatorStateInfo(0).IsName(state), Is.True, "Actual entered nontransitioning state " + state);
        }

        private static void Planted(CharacterPresentation body, CharacterPresentation.FarewellObservation baseline,
            CharacterPresentation.FarewellObservation pose)
        {
            float root = Vector3.Distance(baseline.Root, pose.Root);
            Assert.That(root, Is.LessThanOrEqualTo(FarewellPair.RootTolerance), root <= FarewellPair.RootTolerance ? ""
                : PlantedDiagnostic(body, "Root", baseline.Root, pose.Root, root, pose));
            float heading = Quaternion.Angle(baseline.Heading, pose.Heading);
            Assert.That(heading, Is.LessThanOrEqualTo(FarewellPair.HeadingTolerance), heading <= FarewellPair.HeadingTolerance ? ""
                : PlantedDiagnostic(body, "Heading", baseline.Root, pose.Root, heading, pose,
                    " baselineHeading=" + baseline.Heading.ToString("R") + " actualHeading=" + pose.Heading.ToString("R")));
            var before = new[] { baseline.LeftFoot, baseline.RightFoot, baseline.LeftToes, baseline.RightToes };
            var now = new[] { pose.LeftFoot, pose.RightFoot, pose.LeftToes, pose.RightToes };
            var names = new[] { "LeftFoot", "RightFoot", "LeftToes", "RightToes" };
            var gaps = new[] { Vector3.Distance(baseline.LeftFoot, pose.LeftFoot), Vector3.Distance(baseline.RightFoot, pose.RightFoot),
                Vector3.Distance(baseline.LeftToes, pose.LeftToes), Vector3.Distance(baseline.RightToes, pose.RightToes) };
            for (int i = 0; i < before.Length; i++)
            {
                float gap = gaps[i];
                Assert.That(gap, Is.LessThanOrEqualTo(FarewellPair.FootTolerance), gap <= FarewellPair.FootTolerance ? ""
                    : PlantedDiagnostic(body, names[i], before[i], now[i], gap, pose));
            }
        }

        private static string PlantedDiagnostic(CharacterPresentation body, string landmark, Vector3 baseline, Vector3 actual,
            float gap, CharacterPresentation.FarewellObservation pose, string additional = "")
        {
            var report = "Actual pre-trigger Idle baseline; actor=" + (body != null ? body.CharacterId : "null")
                + " landmark=" + landmark + " baseline=" + baseline.ToString("R") + " actual=" + actual.ToString("R")
                + " gap=" + gap.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + " frame=" + Time.frameCount + " inTake=" + pose.InTake + " idle=" + pose.Idle + " boundary=" + pose.Boundary
                + " progress=" + pose.NormalizedTime.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + " selectedWeight=" + pose.SelectedClipWeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + additional + " " + FarewellNativeDiagnostic(body);
            return report.Length <= 2048 ? report : report.Substring(0, 2048);
        }

        private sealed class SkeletonProvider : IModularCharacterBodyProvider
        {
            public ICharacterAppearanceCatalog Catalog => null;
            public bool TryCreate(in CharacterBodyRequest request, Transform parent, Color badge, out CharacterBody body)
            {
                body = default;
                var model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(Skeleton);
                var avatar = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(Skeleton).OfType<Avatar>().FirstOrDefault();
                var controller = Resources.Load<RuntimeAnimatorController>("Animation/GamesimHumanoid");
                if (model == null || avatar == null || controller == null) return false;
                var root = Object.Instantiate(model, parent, false);
                var animator = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
                animator.avatar = avatar; animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.updateMode = AnimatorUpdateMode.Normal;
                body = new CharacterBody(root, animator, false, this);
                return true;
            }
            public bool TryCreate(string id, Transform parent, Color colour, out CharacterBody body) { body = default; return false; }
            public void SetWardrobeColor(in CharacterBody body, Color colour) { }
        }
    }

    // Fixture-only passive reader. No animation/IK setters and no production companion.
    public sealed class GroundedFarewellPassiveIkObserver : MonoBehaviour
    {
        private const float Window = 2f, AdmissionInterval = 1f / 60f;
        private static readonly HumanBodyBones[] Bones =
            { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes };
        private static readonly AvatarIKGoal[] Goals = { AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot };
        private static readonly System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        private readonly Vector3[] before = new Vector3[4], after = new Vector3[4], goalPosition = new Vector3[2];
        private readonly Quaternion[] beforeRotation = new Quaternion[4], afterRotation = new Quaternion[4], goalRotation = new Quaternion[2];
        private readonly float[] positionWeight = new float[2], rotationWeight = new float[2];
        private readonly Transform[] bones = new Transform[4];
        private readonly System.Collections.Generic.List<MonoBehaviour> currentHost = new System.Collections.Generic.List<MonoBehaviour>(16);
        private MonoBehaviour[] hostBehaviours;
        private bool[] hostEnabled, hostActive;
        private readonly System.Collections.Generic.List<AnimatorClipInfo> selected = new System.Collections.Generic.List<AnimatorClipInfo>(2);
        private CharacterPresentation body;
        private Animator live;
        private AnimationClip clip;
        private object identity;
        private System.Reflection.MethodInfo holds;
        private BodyFrame bodyFrame;
        private AnimatorCullingMode culling;
        private CharacterPresentation.Reaction? reaction;
        private CharacterPresentation.Gesture? gesture;
        private CharacterPresentation.BodyActivity activity;
        private Report report;
        private int bodyIndex, stateHash, armedFrame, callbackFrame = -1, completedFrame = -1;
        private int callbacks, completedFrames, admitted;
        private float started, lastAdmitted = float.NegativeInfinity, initialPhase, phase, previousPhase, callbackTime, callbackWeight, timeScale;
        private Vector3 root, initialRoot;
        private Quaternion heading, initialHeading;
        private bool armed, pending, complete;
        private string failure;
        private float maxSamplePosePosition, maxSamplePoseAngle;

        public static IEnumerator Observe(CharacterPresentation[] bodies, string provenance)
        {
            var readers = new GroundedFarewellPassiveIkObserver[2];
            var output = new Report(provenance);
            bool passed = false;
            try
            {
                Assert.That(bodies.Length, Is.EqualTo(2));
                for (int i = 0; i < 2; i++)
                {
                    Assert.That(bodies[i] != null && !bodies[i].IsPrimitiveBody, Is.True);
                    var animator = bodies[i].VisualRoot.GetComponentInChildren<Animator>(true);
                    Assert.That(animator, Is.Not.Null);
                    readers[i] = animator.gameObject.AddComponent<GroundedFarewellPassiveIkObserver>();
                    readers[i].Bind(bodies[i], animator, i, output);
                }
                // The observation clock starts AFTER both actual identities and baselines exist.
                float start = Time.realtimeSinceStartup;
                foreach (var reader in readers) reader.Arm(start);
                while (Time.realtimeSinceStartup - start < Window)
                {
                    foreach (var reader in readers) Assert.That(reader.failure, Is.Null);
                    yield return null;
                }
                // yield null is NOT assumed to follow LateUpdate: require each reader's own
                // completed same-frame LateUpdate snapshot at/after the requested endpoint.
                yield return null;
                foreach (var reader in readers)
                {
                    Assert.That(reader.failure, Is.Null);
                    Assert.That(reader.complete, Is.True, "No completed native callback/final-pose endpoint.");
                    Assert.That(reader.callbacks, Is.GreaterThan(0));
                    Assert.That(reader.completedFrames, Is.EqualTo(reader.callbacks));
                    Assert.That(reader.admitted, Is.GreaterThan(1));
                    Assert.That(reader.phase, Is.GreaterThan(reader.initialPhase), "Actual selected Idle must advance.");
                }
                passed = true;
            }
            finally
            {
                // Disable the actual callback host BEFORE either fixture destroys its body.
                foreach (var reader in readers)
                    if (reader != null) { reader.armed = false; reader.enabled = false; }
                try { output.Finish(readers, passed); Debug.Log(output.Text); }
                finally { foreach (var reader in readers) if (reader != null) Object.Destroy(reader); }
            }
        }

        private void Bind(CharacterPresentation owner, Animator animator, int index, Report output)
        {
            body = owner; live = animator; bodyIndex = index; report = output;
            var capture = typeof(CharacterPresentation).GetMethod("TryGetFarewellBody", Flags);
            holds = typeof(CharacterPresentation).GetMethod("HoldsFarewellBody", Flags);
            Need(capture != null && holds != null, "identity-api");
            var args = new object[] { null };
            Need((bool)capture.Invoke(body, args), "capture"); identity = args[0];
            Need(identity != null && (Animator)Field("Animator") == live && live.gameObject == gameObject, "actual-animator-host");
            var handle = Resources.Load<AnimatorOverrideController>("Animation/GamesimHumanoid");
            var authority = handle != null ? handle.runtimeAnimatorController : null;
            Need(authority != null, "resources-base-authority");
#if UNITY_EDITOR
            var imported = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
                "Assets/Gamesim/Art/Characters/GamesimHumanoid.controller");
            Need(imported != null && imported.layers.Length == 1 && imported.layers[0].iKPass
                && imported == authority, "actual-base-ikpass");
#endif
            var controller = live.runtimeAnimatorController;
            var overrides = controller as AnimatorOverrideController;
            Need(controller == authority || overrides != null && overrides.runtimeAnimatorController == authority, "controller-lineage");
            Need(body.TryReadFarewell(out var pose) && pose.Idle && !pose.InTake
                && pose.Boundary == CharacterPresentation.FarewellBoundary.None, "actual-idle");
            Need(!live.IsInTransition(0), "initial-transition");
            var state = live.GetCurrentAnimatorStateInfo(0);
            live.GetCurrentAnimatorClipInfo(0, selected);
            Need(state.IsName("Idle") && selected.Count == 1 && selected[0].clip != null
                && Valid(selected[0].weight) && selected[0].weight > 0f && selected[0].weight <= 1f, "initial-selection");
            clip = selected[0].clip; Need(clip.humanMotion && Valid(clip.length) && clip.length > 0f, "actual-human-idle");
            stateHash = state.fullPathHash; initialPhase = previousPhase = state.normalizedTime;
            Need(Valid(initialPhase) && initialPhase >= 0f, "initial-phase");
            for (int i = 0; i < 4; i++) { bones[i] = live.GetBoneTransform(Bones[i]); Need(bones[i] != null, "actual-foot-bone"); }
            bodyFrame = body.Frame; culling = live.cullingMode; reaction = body.LastReaction; gesture = body.LastGesture; activity = body.Activity;
            timeScale = Time.timeScale; Need(Valid(timeScale) && timeScale > 0f, "time-scale");
            initialRoot = body.transform.position; initialHeading = body.transform.rotation;
            Need(Valid(initialRoot) && Valid(initialHeading), "initial-root");
            report.Append("IDENTITY body=" + index + " character=" + body.CharacterId + " frame=" + bodyFrame
                + " visual=" + Id((Object)Field("Visual")) + " provided=" + Id((Object)Field("ProvidedRoot"))
                + " animator=" + Id(live) + " host=" + Id(gameObject) + " controller=" + Id((Object)Field("Controller"))
                + " avatar=" + Id((Object)Field("Avatar")) + " build=" + Id((Object)Field("Build"))
                + " revision=" + Field("RequestRevision") + " clip=" + Id(clip) + " clipName=" + clip.name
                + " clipLength=" + Number(clip.length) + " stateHash=" + stateHash + " initialPhase=" + Number(initialPhase)
                + " culling=" + culling + " rootMotion=" + live.applyRootMotion + " speed=" + Number(live.speed)
                + " updateMode=" + live.updateMode + "\n", false);
            CaptureHostCallbacks();
        }

        private void CaptureHostCallbacks()
        {
            hostBehaviours = live.gameObject.GetComponents<MonoBehaviour>();
            Need(hostBehaviours.Length <= 128, "host-component-cap");
            hostEnabled = new bool[hostBehaviours.Length]; hostActive = new bool[hostBehaviours.Length];
            report.Append("HOST body=" + bodyIndex + " name=" + gameObject.name + " parent="
                + Id(transform.parent) + " components=" + hostBehaviours.Length
                + " onlyWriterClaim=false callbackOrder=unknown inheritedMethodsIncluded=true bridgeOtherHostsUnobserved=true\n", false);
            for (int i = 0; i < hostBehaviours.Length; i++)
            {
                var component = hostBehaviours[i]; Need(component != null, "host-missing-script");
                hostEnabled[i] = component.enabled; hostActive[i] = component.isActiveAndEnabled;
                var type = component.GetType(); var metadata = new System.Text.StringBuilder(512); int methods = 0, depth = 0;
                for (var owner = type; owner != null && typeof(MonoBehaviour).IsAssignableFrom(owner); owner = owner.BaseType)
                {
                    Need(++depth <= 32, "host-inheritance-cap");
                    foreach (var method in owner.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                    {
                        if (method.Name != "OnAnimatorIK") continue;
                        Need(++methods <= 32, "host-callback-metadata-cap");
                        var parameters = method.GetParameters();
                        metadata.Append(" declaring=").Append(owner.FullName).Append(" token=").Append(method.MetadataToken)
                            .Append(" public=").Append(method.IsPublic ? "true" : "false")
                            .Append(" callbackShape=").Append(method.ReturnType == typeof(void) && parameters.Length == 1
                                && parameters[0].ParameterType == typeof(int) ? "true" : "false");
                    }
                }
                report.Append("BEHAVIOUR body=" + bodyIndex + " index=" + i + " id=" + Id(component)
                    + " type=" + type.FullName + " enabled=" + (hostEnabled[i] ? "true" : "false")
                    + " active=" + (hostActive[i] ? "true" : "false") + " ikMethodCount=" + methods + metadata + "\n", false);
            }
        }

        private object Field(string name)
        {
            var field = identity.GetType().GetField(name, Flags);
            Need(field != null, "identity-field"); return field.GetValue(identity);
        }

        private void Arm(float start) { started = start; armedFrame = Time.frameCount; armed = true; }

        private float CheckSelection()
        {
            Need(body != null && live != null && clip != null && bones.All(bone => bone != null), "live-object");
            Need((bool)holds.Invoke(body, new[] { identity }) && body.Frame == bodyFrame && live.cullingMode == culling, "identity-held");
            currentHost.Clear(); live.GetComponents(currentHost);
            Need(currentHost.Count == hostBehaviours.Length, "host-component-identity");
            for (int i = 0; i < hostBehaviours.Length; i++)
                Need(currentHost[i] == hostBehaviours[i] && currentHost[i] != null
                    && currentHost[i].enabled == hostEnabled[i] && currentHost[i].isActiveAndEnabled == hostActive[i], "host-behaviour-held");
            Need(body.LastReaction == reaction && body.LastGesture == gesture && body.Activity == activity
                && Time.timeScale == timeScale, "untouched-presentation");
            Need(!live.IsInTransition(0), "transition");
            var state = live.GetCurrentAnimatorStateInfo(0); selected.Clear(); live.GetCurrentAnimatorClipInfo(0, selected);
            Need(state.IsName("Idle") && state.fullPathHash == stateHash && selected.Count == 1 && selected[0].clip == clip
                && Valid(selected[0].weight) && selected[0].weight > 0f && selected[0].weight <= 1f, "selected-idle");
            Need(Valid(state.normalizedTime) && state.normalizedTime >= previousPhase, "natural-phase");
            return state.normalizedTime;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (!armed || complete || failure != null) return;
            callbacks++; // Count the actual invocation even when its reads refuse.
            try
            {
                Need(layerIndex == 0 && !pending && callbackFrame != Time.frameCount, "callback-pairing");
                phase = CheckSelection(); previousPhase = phase; callbackWeight = selected[0].weight;
                callbackTime = Time.realtimeSinceStartup - started; Need(Valid(callbackTime) && callbackTime >= 0f, "callback-time");
                root = body.transform.position; heading = body.transform.rotation;
                Need(Valid(root) && Valid(heading), "callback-root");
                for (int i = 0; i < 4; i++)
                {
                    before[i] = bones[i].position; beforeRotation[i] = bones[i].rotation;
                    Need(Valid(before[i]) && Valid(beforeRotation[i]), "pre-ik-bone");
                }
                for (int i = 0; i < 2; i++)
                {
                    goalPosition[i] = live.GetIKPosition(Goals[i]); goalRotation[i] = live.GetIKRotation(Goals[i]);
                    positionWeight[i] = live.GetIKPositionWeight(Goals[i]); rotationWeight[i] = live.GetIKRotationWeight(Goals[i]);
                    Need(Valid(goalPosition[i]) && FiniteQuaternion(goalRotation[i]) && Valid(positionWeight[i]) && Valid(rotationWeight[i])
                        && positionWeight[i] >= 0f && positionWeight[i] <= 1f && rotationWeight[i] >= 0f && rotationWeight[i] <= 1f, "actual-ik-goal");
                }
                callbackFrame = Time.frameCount; pending = true;
            }
            catch (System.Exception error) { Refuse("callback", error); }
        }

        private void LateUpdate()
        {
            if (!armed || complete || failure != null) return;
            if (Time.frameCount == armedFrame && !pending) return; // Bind can follow this frame's animation.
            try
            {
                Need(pending && callbackFrame == Time.frameCount && completedFrame != Time.frameCount, "same-frame-final-pose");
                float finalPhase = CheckSelection();
                Need(finalPhase == phase, "same-evaluation-phase"); // No later animation evaluation is substituted.
                float elapsed = Time.realtimeSinceStartup - started;
                Need(Valid(elapsed) && elapsed >= callbackTime, "final-time");
                var finalRoot = body.transform.position; var finalHeading = body.transform.rotation;
                Need(Valid(finalRoot) && Valid(finalHeading), "final-root");
                float posePosition = 0f, poseAngle = 0f;
                for (int i = 0; i < 4; i++)
                {
                    after[i] = bones[i].position; afterRotation[i] = bones[i].rotation;
                    Need(Valid(after[i]) && Valid(afterRotation[i]), "final-bone");
                    posePosition = Mathf.Max(posePosition, Vector3.Distance(before[i], after[i]));
                    poseAngle = Mathf.Max(poseAngle, Quaternion.Angle(beforeRotation[i], afterRotation[i]));
                }
                float rootDrift = Vector3.Distance(initialRoot, finalRoot), headingDrift = Quaternion.Angle(initialHeading, finalHeading);
                Need(Valid(posePosition) && Valid(poseAngle) && Valid(rootDrift) && Valid(headingDrift), "mapping-finite");
                bool samePose = posePosition <= .00001f && poseAngle <= .01f
                    && Vector3.Distance(root, finalRoot) <= .00001f && Quaternion.Angle(heading, finalHeading) <= .01f;
                // All natural frames are checked; only sparse, SAME-frame snapshots enter the report.
                bool admit = elapsed >= lastAdmitted + AdmissionInterval || elapsed >= Window;
                if (admit)
                {
                    var row = new System.Text.StringBuilder(1536);
                    row.Append("ROW body=").Append(bodyIndex).Append(" ordinal=").Append(callbacks)
                        .Append(" callbackFrame=").Append(callbackFrame).Append(" lateFrame=").Append(Time.frameCount)
                        .Append(" callbackElapsed=").Append(Number(callbackTime)).Append(" lateElapsed=").Append(Number(elapsed))
                        .Append(" phase=").Append(Number(phase)).Append(" callbackClipWeight=").Append(Number(callbackWeight))
                        .Append(" lateClipWeight=").Append(Number(selected[0].weight))
                        .Append(" identityHeld=true setters=0 samePose=").Append(samePose ? "true" : "false")
                        .Append(" posePositionError=").Append(Number(posePosition)).Append(" poseAngleError=").Append(Number(poseAngle))
                        .Append(" rootDrift=").Append(Number(rootDrift)).Append(" headingDrift=").Append(Number(headingDrift))
                        .Append(" callbackRoot="); Vector(row, root); row.Append(" callbackHeading="); Rotation(row, heading);
                    row.Append(" lateRoot="); Vector(row, finalRoot); row.Append(" lateHeading="); Rotation(row, finalHeading);
                    for (int i = 0; i < 2; i++)
                    {
                        row.Append(" goal").Append(i).Append("="); Vector(row, goalPosition[i]); row.Append(","); Rotation(row, goalRotation[i]);
                        row.Append(",").Append(Number(positionWeight[i])).Append(",").Append(Number(rotationWeight[i]));
                        bool usableRotation = Valid(goalRotation[i]);
                        row.Append(" goalRotationUsable").Append(i).Append("=").Append(usableRotation ? "true" : "false");
                        row.Append(" goalFootOffset").Append(i).Append("=");
                        if (usableRotation)
                        {
                            var offset = Quaternion.Inverse(goalRotation[i]) * (before[i] - goalPosition[i]);
                            var offsetRotation = Quaternion.Inverse(goalRotation[i]) * beforeRotation[i];
                            Need(Valid(offset) && Valid(offsetRotation), "goal-foot-offset-finite");
                            Vector(row, offset); row.Append(","); Rotation(row, offsetRotation);
                        }
                        else row.Append("unavailable"); // Finite raw zero quaternion remains observed, never fitted.
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        row.Append(" pre").Append(i).Append("="); Vector(row, before[i]); row.Append(","); Rotation(row, beforeRotation[i]);
                        row.Append(" late").Append(i).Append("="); Vector(row, after[i]); row.Append(","); Rotation(row, afterRotation[i]);
                    }
                    row.Append("\n"); report.Append(row.ToString(), true);
                    // Only fully admitted finite rows contribute to these sparse maxima.
                    admitted++; lastAdmitted = elapsed;
                    maxSamplePosePosition = Mathf.Max(maxSamplePosePosition, posePosition);
                    maxSamplePoseAngle = Mathf.Max(maxSamplePoseAngle, poseAngle);
                }
                completedFrame = Time.frameCount; completedFrames++; pending = false;
                Need(rootDrift <= .001f && headingDrift <= .05f, "unchanged-root-heading");
                if (elapsed >= Window) complete = true;
            }
            catch (System.Exception error) { Refuse("late", error); }
        }

        private void Refuse(string stage, System.Exception error)
        { if (failure == null) failure = stage + ":" + (error is Refusal refusal ? refusal.Stage : error.GetType().Name); }
        private sealed class Refusal : System.Exception
        { public readonly string Stage; public Refusal(string stage) { Stage = stage; } }
        private static void Need(bool condition, string stage) { if (!condition) throw new Refusal(stage); }
        private static bool Valid(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Valid(Vector3 value) => Valid(value.x) && Valid(value.y) && Valid(value.z);
        private static bool FiniteQuaternion(Quaternion value) => Valid(value.x) && Valid(value.y) && Valid(value.z) && Valid(value.w);
        private static bool Valid(Quaternion value) => FiniteQuaternion(value)
            && Valid(value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w)
            && value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w > .000001f;
        private static string Number(float value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        private static string Id(Object value) => value == null ? "null" : value.GetEntityId().ToString();
        private static void Vector(System.Text.StringBuilder text, Vector3 value)
            => text.Append(Number(value.x)).Append(",").Append(Number(value.y)).Append(",").Append(Number(value.z));
        private static void Rotation(System.Text.StringBuilder text, Quaternion value)
            => text.Append(Number(value.x)).Append(",").Append(Number(value.y)).Append(",").Append(Number(value.z)).Append(",").Append(Number(value.w));

        private sealed class Report
        {
            private const int MaxRows = 4096, MaxBytes = 4 * 1024 * 1024, Reserve = 4096;
            private readonly System.Text.StringBuilder text = new System.Text.StringBuilder(4096);
            private int bytes, rows;
            public string Text => text.ToString();
            public Report(string provenance)
            {
                Append("PASSIVE-IK version=1 provenance=" + provenance + " durationRequested=2 sparseSeconds=1/60"
                    + " boneOrder=LeftFoot,RightFoot,LeftToes,RightToes goalOrder=LeftFoot,RightFoot"
                    + " globalIdleDriven=false setterCount=0 lateIsWholeFrameFinal=false writerOrder=unknown calibrationQualified=false planted=false\n", false);
            }
            public void Append(string value, bool row)
            {
                int size = System.Text.Encoding.UTF8.GetByteCount(value);
                Need((!row || rows < MaxRows) && size <= MaxBytes - Reserve - bytes, "report-cap");
                text.Append(value); bytes += size; if (row) rows++;
            }
            public void Finish(GroundedFarewellPassiveIkObserver[] readers, bool passed)
            {
                var last = new System.Text.StringBuilder(1024);
                last.Append("SUMMARY passed=").Append(passed ? "true" : "false").Append(" admittedRows=").Append(rows)
                    .Append(" sparseMaximaOnly=true calibrationQualified=false planted=false setterCount=0\n");
                for (int i = 0; i < readers.Length; i++)
                {
                    var reader = readers[i]; if (reader == null) { last.Append("BODY ").Append(i).Append(" absent=true\n"); continue; }
                    last.Append("BODY ").Append(i).Append(" callbacks=").Append(reader.callbacks).Append(" sameFrameLateSnapshots=").Append(reader.completedFrames)
                        .Append(" admitted=").Append(reader.admitted).Append(" complete=").Append(reader.complete ? "true" : "false")
                        .Append(" phaseInitial=").Append(Number(reader.initialPhase)).Append(" phaseAtLastCallback=").Append(Number(reader.phase))
                        .Append(" sparseMaxPosePositionError=").Append(Number(reader.maxSamplePosePosition))
                        .Append(" sparseMaxPoseAngleError=").Append(Number(reader.maxSamplePoseAngle))
                        .Append(" failure=").Append(reader.failure ?? "none").Append("\n");
                }
                string tail = last.ToString(); int size = System.Text.Encoding.UTF8.GetByteCount(tail);
                Need(size <= Reserve && bytes + size <= MaxBytes, "report-tail-cap"); text.Append(tail); bytes += size;
            }
        }
    }
    /// <summary>Read-only public topology, not an inserted job or controller-lineage qualification.</summary>
    internal static class GroundedFarewellStreamTopologyReader
    {
        private const int Nodes = 64, Edges = 128, Roots = 16, Outputs = 16, Text = 256, Bytes = 64 * 1024, Tail = 512;
        private const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        private sealed class Reader
        {
            internal CharacterPresentation body;
            internal Animator animator;
            internal object identity;
            internal System.Reflection.MethodInfo holds;
            internal RuntimeAnimatorController controller, authority;
            internal AnimationClip clip;
            internal int state, frame;
            internal float phase;
            internal Vector3 root;
            internal Quaternion heading;
            internal UnityEngine.Playables.PlayableGraph graph;
            internal readonly System.Collections.Generic.List<UnityEngine.Playables.Playable> handles = new System.Collections.Generic.List<UnityEngine.Playables.Playable>();
            internal readonly System.Collections.Generic.List<UnityEngine.Playables.PlayableOutput> outputHandles = new System.Collections.Generic.List<UnityEngine.Playables.PlayableOutput>();
            internal readonly System.Text.StringBuilder report = new System.Text.StringBuilder();
            internal int bytes;
            internal string stage = "capture";
            internal bool complete;
            internal void Line(string line)
            {
                int size = System.Text.Encoding.UTF8.GetByteCount(line + "\n");
                Need(bytes + size <= Bytes - Tail, "report-cap"); report.Append(line).Append('\n'); bytes += size;
            }
            internal void Finish(int index)
            {
                string tail = "END body=" + index + " observationComplete=" + complete
                    + " stage=" + stage + " setters=0 graphMutation=0 insertionFeasible=false controllerObjectLineageQualified=false"
                    + " enumerationIsWriterOrder=false wholeFrameFinal=false otherHostsUnobserved=true\n";
                Need(System.Text.Encoding.UTF8.GetByteCount(tail) <= Tail, "tail-cap");
                Debug.Log("FAREWELL_STREAM_TOPOLOGY_V1\n" + report + tail);
            }
        }
        private sealed class Snapshot
        {
            internal bool valid, playing;
            internal int playableCount, roots, outputs, edges;
            internal string mode;
            internal readonly System.Collections.Generic.List<int> visited = new System.Collections.Generic.List<int>();
            internal readonly System.Collections.Generic.List<int> rootIds = new System.Collections.Generic.List<int>();
            internal readonly System.Collections.Generic.List<string> signature = new System.Collections.Generic.List<string>();
            internal readonly bool[,] connections = new bool[Nodes, Nodes];
            internal readonly bool[] controllerTypes = new bool[Nodes];
            internal readonly System.Collections.Generic.List<int> outputSources = new System.Collections.Generic.List<int>();
        }
        internal static IEnumerator Observe(CharacterPresentation[] bodies, string provenance)
        {
            Need(bodies != null && bodies.Length == 2, "two-real-bodies");
            var readers = new Reader[2];
            try
            {
                var before = new Snapshot[2];
                for (int i = 0; i < 2; i++)
                {
                    readers[i] = new Reader { body = bodies[i] }; Capture(readers[i]); readers[i].Line("BEGIN body=" + i + " provenance=" + Bounded(provenance));
                    before[i] = Scan(readers[i], "before"); readers[i].stage = "wait-natural-frame";
                }
                yield return null; // No graph/Animator evaluation or phase drive. This is not a whole-frame-final boundary.
                for (int i = 0; i < 2; i++)
                {
                    var reader = readers[i]; reader.stage = "later-identity-idle";
                    float later = Selection(reader);
                    Need(Time.frameCount > reader.frame && later > reader.phase, "natural-idle-did-not-advance");
                    Need(reader.animator.playableGraph.Equals(reader.graph), "actual-graph-identity-changed");
                    var after = Scan(reader, "later");
                    Need(before[i].valid == after.valid && before[i].playing == after.playing && before[i].mode == after.mode
                        && before[i].playableCount == after.playableCount && before[i].roots == after.roots && before[i].outputs == after.outputs
                        && before[i].signature.SequenceEqual(after.signature), "actual-topology-changed");
                    reader.Line("WINDOW initialFrame=" + reader.frame + " laterFrame=" + Time.frameCount
                        + " initialPhase=" + Number(reader.phase) + " laterPhase=" + Number(later)
                        + " rootDrift=" + Number(Vector3.Distance(reader.root, reader.body.transform.position))
                        + " headingDrift=" + Number(Quaternion.Angle(reader.heading, reader.body.transform.rotation))
                        + " sameActualGraph=true sameObservedTopology=true nativeIdleAdvanced=true");
                    reader.complete = true; reader.stage = "observation-complete-not-feasibility";
                }
            }
            finally
            {
                // Readers are managed observations only: never destroy/release the graph, Animator, controller or body.
                for (int i = 0; i < readers.Length; i++) if (readers[i] != null) readers[i].Finish(i);
            }
        }
        private static void Capture(Reader reader)
        {
            var body = reader.body; Need(body != null, "body-null");
            var capture = typeof(CharacterPresentation).GetMethod("TryGetFarewellBody", Flags);
            reader.holds = typeof(CharacterPresentation).GetMethod("HoldsFarewellBody", Flags);
            Need(capture != null && reader.holds != null, "body-identity-api");
            var captured = new object[] { null };
            Need((bool)capture.Invoke(body, captured) && captured[0] != null, "genuine-provided-body");
            reader.identity = captured[0]; reader.animator = (Animator)Field(reader, "Animator");
            reader.controller = (RuntimeAnimatorController)Field(reader, "Controller");
            var handle = Resources.Load<AnimatorOverrideController>("Animation/GamesimHumanoid");
            reader.authority = handle != null ? handle.runtimeAnimatorController : null;
            Need(reader.authority != null && reader.animator != null && reader.controller != null, "resources-and-body-authority");
            var overrides = reader.controller as AnimatorOverrideController;
            Need(reader.controller == reader.authority || overrides != null && overrides.runtimeAnimatorController == reader.authority, "captured-controller-lineage");
            reader.root = body.transform.position; reader.heading = body.transform.rotation; reader.frame = Time.frameCount;
            Need(Finite(reader.root) && Finite(reader.heading), "initial-root-heading");
            reader.phase = Selection(reader); reader.graph = reader.animator.playableGraph;
            reader.Line("IDENTITY character=" + Bounded(body.CharacterId) + " frame=" + body.Frame
                + " visual=" + Id((UnityEngine.Object)Field(reader, "Visual")) + " provided=" + Id((UnityEngine.Object)Field(reader, "ProvidedRoot"))
                + " animator=" + Id(reader.animator) + " host=" + Id(reader.animator.gameObject) + " controller=" + Id(reader.controller)
                + " resourcesBase=" + Id(reader.authority) + " avatar=" + Id((UnityEngine.Object)Field(reader, "Avatar"))
                + " build=" + Id((UnityEngine.Object)Field(reader, "Build")) + " revision=" + Field(reader, "RequestRevision")
                + " clip=" + Id(reader.clip) + " clipName=" + Bounded(reader.clip.name) + " length=" + Number(reader.clip.length)
                + " root=" + Vector(reader.root) + " heading=" + QuaternionText(reader.heading));
        }
        private static float Selection(Reader reader)
        {
            Need(reader.body != null && reader.animator != null && (bool)reader.holds.Invoke(reader.body, new[] { reader.identity }), "captured-body-not-held");
            Need(reader.animator.runtimeAnimatorController == reader.controller, "controller-replaced");
            Need(reader.body.TryReadFarewell(out var pose) && pose.Idle && !pose.InTake
                && pose.Boundary == CharacterPresentation.FarewellBoundary.None && !reader.animator.IsInTransition(0), "actual-settled-idle");
            var state = reader.animator.GetCurrentAnimatorStateInfo(0);
            var clips = reader.animator.GetCurrentAnimatorClipInfo(0);
            Need(state.IsName("Idle") && clips.Length == 1 && clips[0].clip != null && Finite(clips[0].weight)
                && clips[0].weight > 0f && clips[0].weight <= 1f, "actual-selected-idle");
            Need(clips[0].clip.humanMotion && Finite(clips[0].clip.length) && clips[0].clip.length > 0f
                && Finite(state.normalizedTime) && state.normalizedTime >= 0f, "human-idle-phase");
            if (reader.clip == null) { reader.clip = clips[0].clip; reader.state = state.fullPathHash; }
            Need(clips[0].clip == reader.clip && state.fullPathHash == reader.state, "native-selected-clip-changed");
            Need(Finite(reader.body.transform.position) && Finite(reader.body.transform.rotation), "root-heading-finite");
            Need(Vector3.Distance(reader.root, reader.body.transform.position) <= .001f
                && Quaternion.Angle(reader.heading, reader.body.transform.rotation) <= .05f, "root-heading-drift");
            return state.normalizedTime;
        }
        private static Snapshot Scan(Reader reader, string boundary)
        {
            reader.stage = boundary + "-topology";
            Selection(reader); var graph = reader.animator.playableGraph;
            Need(graph.Equals(reader.graph), "graph-lineage-replaced");
            var scan = new Snapshot { valid = graph.IsValid() };
            reader.Line("GRAPH boundary=" + boundary + " valid=" + scan.valid + " sameCapturedValue=true");
            if (!scan.valid)
            {
                scan.signature.Add("invalid");
                reader.Line("CLASSIFICATION invalidGraph=true observationOnly=true insertionFeasible=false");
                Hosts(reader, boundary); return scan;
            }
            scan.playing = graph.IsPlaying(); scan.mode = Bounded(graph.GetTimeUpdateMode().ToString());
            scan.playableCount = graph.GetPlayableCount(); scan.roots = graph.GetRootPlayableCount(); scan.outputs = graph.GetOutputCount();
            Need(scan.playableCount >= 0 && scan.roots >= 0 && scan.roots <= Roots && scan.outputs >= 0 && scan.outputs <= Outputs, "graph-count-cap");
            reader.Line("COUNTS boundary=" + boundary + " playing=" + scan.playing + " mode=" + scan.mode
                + " playables=" + scan.playableCount + " roots=" + scan.roots + " outputs=" + scan.outputs);
            for (int i = 0; i < scan.roots; i++)
            {
                int id = Admit(reader, scan, graph.GetRootPlayable(i)); scan.rootIds.Add(id);
                Emit(reader, scan, "ROOT index=" + i + " node=" + id);
            }
            int ownAnimationOutputs = 0;
            for (int i = 0; i < scan.outputs; i++)
            {
                var output = graph.GetOutput(i);
                bool isNull = output.IsOutputNull(), valid = output.IsOutputValid();
                int oid = OutputId(reader, output); string type = valid ? TypeText(output.GetPlayableOutputType()) : "unavailable";
                int source = -3, port = -1; string target = "null"; bool targetIsCaptured = false;
                if (valid)
                {
                    source = Admit(reader, scan, output.GetSourcePlayable()); port = output.GetSourceOutputPort();
                    Need(Finite(output.GetWeight()), "output-weight");
                    if (output.IsPlayableOutputOfType<UnityEngine.Animations.AnimationPlayableOutput>())
                    {
                        var animation = (UnityEngine.Animations.AnimationPlayableOutput)output;
                        var actual = animation.GetTarget(); target = Id(actual); targetIsCaptured = actual == reader.animator;
                        if (targetIsCaptured) ownAnimationOutputs++;
                    }
                }
                scan.outputSources.Add(source);
                Emit(reader, scan, "OUTPUT index=" + i + " identity=" + oid + " null=" + isNull + " valid=" + valid + " type=" + type
                    + " sourceNode=" + source + " sourceOutputPort=" + port + " animatorTarget=" + target
                    + " targetIsCaptured=" + targetIsCaptured + " capturedControllerObjectMatch=unobservablePublicApi");
            }
            for (int cursor = 0; cursor < scan.visited.Count; cursor++)
            {
                int id = scan.visited[cursor]; var node = reader.handles[id];
                int inputs = node.GetInputCount(), outputs = node.GetOutputCount();
                Need(inputs >= 0 && outputs >= 0 && inputs <= Edges && outputs <= Edges, "node-port-cap");
                scan.controllerTypes[id] = node.IsPlayableOfType<UnityEngine.Animations.AnimatorControllerPlayable>();
                Emit(reader, scan, "NODE identity=" + id + " type=" + TypeText(node.GetPlayableType())
                    + " inputCount=" + inputs + " outputCount=" + outputs + " sameCapturedGraph=" + node.GetGraph().Equals(graph));
                for (int p = 0; p < inputs; p++)
                {
                    Need(++scan.edges <= Edges, "edge-cap");
                    var actual = node.GetInput(p); bool repeated = Find(reader.handles, actual) >= 0;
                    int source = Admit(reader, scan, actual); float weight = node.GetInputWeight(p); Need(Finite(weight), "input-weight");
                    if (source >= 0) scan.connections[source, id] = true;
                    string inputIdentity = "INPUT destinationNode=" + id + " destinationInputPort=" + p + " sourceNode=" + source
                        + " sourceOutputPort=unexposedPublicApi null=" + actual.IsNull() + " valid=" + actual.IsValid();
                    scan.signature.Add(inputIdentity);
                    reader.Line("INPUT destinationNode=" + id + " destinationInputPort=" + p + " sourceNode=" + source
                        + " sourceOutputPort=unexposedPublicApi null=" + actual.IsNull() + " valid=" + actual.IsValid()
                        + " weight=" + Number(weight) + " seenHandleAgain=" + repeated);
                }
                for (int p = 0; p < outputs; p++)
                {
                    Need(++scan.edges <= Edges, "edge-cap");
                    var actual = node.GetOutput(p); bool repeated = Find(reader.handles, actual) >= 0;
                    int destination = Admit(reader, scan, actual);
                    if (destination >= 0) scan.connections[id, destination] = true;
                    string outputIdentity = "DESTINATION sourceNode=" + id + " sourceOutputPort=" + p + " destinationNode=" + destination
                        + " destinationInputPort=unexposedPublicApi null=" + actual.IsNull() + " valid=" + actual.IsValid();
                    scan.signature.Add(outputIdentity);
                    reader.Line("DESTINATION sourceNode=" + id + " sourceOutputPort=" + p + " destinationNode=" + destination
                        + " destinationInputPort=unexposedPublicApi null=" + actual.IsNull() + " valid=" + actual.IsValid()
                        + " seenHandleAgain=" + repeated);
                }
            }
            bool cycle = false;
            for (int k = 0; k < Nodes; k++) for (int i = 0; i < Nodes; i++) for (int j = 0; j < Nodes; j++)
                scan.connections[i, j] |= scan.connections[i, k] && scan.connections[k, j];
            for (int i = 0; i < Nodes; i++) cycle |= scan.connections[i, i];
            for (int o = 0; o < scan.outputSources.Count; o++)
            {
                int source = scan.outputSources[o], controllerNodes = 0;
                if (source >= 0) for (int n = 0; n < Nodes; n++)
                    if (scan.controllerTypes[n] && (n == source || scan.connections[n, source])) controllerNodes++;
                reader.Line("OUTPUT_LINEAGE index=" + o + " reachableControllerPlayableTypes=" + controllerNodes
                    + " capturedControllerObjectMatch=unobservablePublicApi qualification=false");
            }
            reader.Line("CLASSIFICATION boundary=" + boundary + " reachedNodes=" + scan.visited.Count + " portEdges=" + scan.edges
                + " cycleObserved=" + cycle + " capturedAnimatorOutputs=" + ownAnimationOutputs
                + " missingCapturedAnimatorOutput=" + (ownAnimationOutputs == 0)
                + " controllerObjectLineage=opaquePublicApi insertionFeasible=false");
            Hosts(reader, boundary); return scan;
        }
        private static int Admit(Reader reader, Snapshot scan, UnityEngine.Playables.Playable node)
        {
            if (node.IsNull()) return -1;
            if (!node.IsValid()) return -2;
            Need(node.GetGraph().Equals(reader.graph), "reachable-foreign-graph");
            int id = Find(reader.handles, node);
            if (id < 0) { Need(reader.handles.Count < Nodes, "visited-cap"); id = reader.handles.Count; reader.handles.Add(node); }
            if (!scan.visited.Contains(id)) scan.visited.Add(id);
            return id;
        }
        private static int Find(System.Collections.Generic.List<UnityEngine.Playables.Playable> list, UnityEngine.Playables.Playable value)
        { for (int i = 0; i < list.Count; i++) if (list[i].Equals(value)) return i; return -1; }
        private static int OutputId(Reader reader, UnityEngine.Playables.PlayableOutput output)
        {
            for (int i = 0; i < reader.outputHandles.Count; i++) if (reader.outputHandles[i].Equals(output)) return i;
            Need(reader.outputHandles.Count < Outputs, "output-identity-cap"); reader.outputHandles.Add(output); return reader.outputHandles.Count - 1;
        }
        private static void Emit(Reader reader, Snapshot scan, string line) { scan.signature.Add(line); reader.Line(line); }
        private static void Hosts(Reader reader, string boundary)
        {
            var components = reader.animator.gameObject.GetComponents<MonoBehaviour>(); Need(components.Length <= 128, "host-cap");
            reader.Line("HOST boundary=" + boundary + " components=" + components.Length
                + " callbackOrder=unknown exclusiveWriter=false inheritedMethodsObserved=true otherHostsUnobserved=true");
            foreach (var component in components)
            {
                Need(component != null, "host-missing-script"); int depth = 0, methods = 0;
                reader.Line("BEHAVIOUR id=" + Id(component) + " type=" + TypeText(component.GetType())
                    + " enabled=" + component.enabled + " active=" + component.isActiveAndEnabled);
                for (var owner = component.GetType(); owner != null && typeof(MonoBehaviour).IsAssignableFrom(owner); owner = owner.BaseType)
                {
                    Need(++depth <= 32, "inheritance-cap");
                    foreach (var method in owner.GetMethods(Flags | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly))
                    {
                        if (method.Name != "OnAnimatorIK" && method.Name != "LateUpdate") continue;
                        Need(++methods <= 32, "callback-cap");
                        reader.Line("CALLBACK declaring=" + TypeText(owner) + " name=" + Bounded(method.Name)
                            + " token=" + method.MetadataToken + " public=" + method.IsPublic + " invoked=false");
                    }
                }
            }
        }
        private static object Field(Reader reader, string name)
        { var field = reader.identity.GetType().GetField(name, Flags); Need(field != null, "identity-field"); return field.GetValue(reader.identity); }
        private static string TypeText(System.Type type) => type == null ? "unavailable" : Bounded(type.FullName);
        private static string Bounded(string value)
        { Need(value != null && value.Length <= Text && !value.Any(char.IsControl), "text-cap"); return value; }
        private static string Id(UnityEngine.Object value) => value != null ? value.GetEntityId().ToString() : "null";
        private static string Number(float value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        private static string Vector(Vector3 value) => Number(value.x) + "," + Number(value.y) + "," + Number(value.z);
        private static string QuaternionText(Quaternion value) => Number(value.x) + "," + Number(value.y) + "," + Number(value.z) + "," + Number(value.w);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w)
            && value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w > 1e-8f;
        private static void Need(bool value, string code) { if (!value) throw new System.InvalidOperationException("stream-topology:" + code); }
    }

    /// <summary>Stateless attachment/restoration only; no invocation, pose equivalence or planting claim.</summary>
    internal static class GroundedFarewellInertStreamAdapter
    {
        private const int Nodes = 64, Edges = 128, Roots = 16, Outputs = 16, Text = 256, Bytes = 64 * 1024, Tail = 2048;
        private const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        private struct EmptyJob : UnityEngine.Animations.IAnimationJob
        {
            public void ProcessAnimation(UnityEngine.Animations.AnimationStream stream) { }
            public void ProcessRootMotion(UnityEngine.Animations.AnimationStream stream) { }
        }
        private sealed class Node
        {
            internal Playable handle;
            internal System.Type type;
            internal Playable[] inputs, outputs;
            internal float[] weights;
        }
        private sealed class Output
        {
            internal PlayableOutput handle;
            internal System.Type type;
            internal Playable source;
            internal int port;
            internal float weight;
            internal Animator target;
            internal UnityEngine.Object reference, user;
        }
        private sealed class Snapshot
        {
            internal readonly System.Collections.Generic.List<Node> nodes = new System.Collections.Generic.List<Node>();
            internal readonly System.Collections.Generic.List<Output> outputs = new System.Collections.Generic.List<Output>();
            internal readonly System.Collections.Generic.List<Playable> roots = new System.Collections.Generic.List<Playable>();
            internal int edges;
        }
        private sealed class Reader
        {
            internal CharacterPresentation body;
            internal Animator animator;
            internal object identity;
            internal System.Reflection.MethodInfo holds;
            internal RuntimeAnimatorController controller, authority;
            internal AnimatorCullingMode culling;
            internal AnimationClip clip;
            internal int state, frame, lastFrame;
            internal float phase, lastPhase, createdWeight;
            internal Vector3 root;
            internal Quaternion heading;
            internal PlayableGraph graph;
            internal Snapshot baseline;
            internal Output output;
            internal UnityEngine.Animations.AnimationScriptPlayable job;
            internal Playable owned;
            internal bool returned, createAttempt, connectAttempt, weightAttempt, swapAttempt, restoreAttempt, disconnectAttempt, destroyAttempt;
            internal bool connectionReached, swapReached, weightReached, outputRestorationReached, disconnectionReached;
            internal bool restored, complete, cleanupBlocked;
            internal bool swapBoundaryQualified, restorationBoundaryQualified;
            internal bool bodyRefusalRecorded;
            internal bool? retainedGraphValid, retainedOwnedValid, retainedBodyHeld;
            internal string stage = "capture", containment = "no-owned-node", cleanupFailure = "none", cleanupRefusal = "none";
            internal readonly System.Text.StringBuilder report = new System.Text.StringBuilder();
            internal int bytes;
            internal void Line(string line)
            {
                int size = System.Text.Encoding.UTF8.GetByteCount(line + "\n");
                Need(bytes + size <= Bytes - Tail, "report-cap");
                report.Append(line).Append('\n'); bytes += size;
            }
            internal void Finish(int index)
            {
                string tail = "END body=" + index + " stage=" + stage + " attachmentRestorationComplete=" + complete
                    + " returnedOwnedHandle=" + returned + " createAttempt=" + createAttempt + " connectAttempt=" + connectAttempt
                    + " weightAttempt=" + weightAttempt + " swapAttempt=" + swapAttempt + " restoreAttempt=" + restoreAttempt
                    + " disconnectAttempt=" + disconnectAttempt + " destroyAttempt=" + destroyAttempt
                    + " connectionReached=" + connectionReached + " swapReached=" + swapReached + " weightReached=" + weightReached
                    + " outputRestorationReached=" + outputRestorationReached + " disconnectionReached=" + disconnectionReached
                    + " exactRestoration=" + restored + " cleanupBlocked=" + cleanupBlocked + " containment=" + containment
                    + " swapBoundaryQualified=" + swapBoundaryQualified + " restorationBoundaryQualified=" + restorationBoundaryQualified
                    + " cleanupFailureType=" + cleanupFailure + " cleanupRefusal=" + cleanupRefusal
                    + " retainedGraphValid=" + Fact(retainedGraphValid) + " retainedOwnedValid=" + Fact(retainedOwnedValid)
                    + " retainedBodyHeld=" + Fact(retainedBodyHeld)
                    + " invocationObserved=unobservable passThroughEquivalence=false controllerObjectLineageQualified=false"
                    + " writerOrderQualified=false wholeFrameFinal=false plantingAcceptance=false noninterferenceAcceptance=false"
                    + " originalFixtureTeardownIsImmediateGraphReclamationProof=false\n";
                const string header = "FAREWELL_INERT_ATTACHMENT_V1\n";
                Need(System.Text.Encoding.UTF8.GetByteCount(header + tail) <= Tail, "tail-cap");
                Debug.Log(header + report + tail);
            }
        }
        private sealed class Reached
        {
            internal Snapshot scan;
            internal bool ownedValid, connected, swapped;
        }
        internal static IEnumerator Observe(CharacterPresentation[] bodies, string provenance)
        {
            Need(bodies != null && bodies.Length == 2, "two-genuine-bodies");
            var readers = new Reader[2];
            try
            {
                // Both actual bodies and their complete public graphs are captured BEFORE either write.
                for (int i = 0; i < readers.Length; i++)
                {
                    readers[i] = new Reader { body = bodies[i] };
                    Capture(readers[i], i, provenance);
                }
                for (int frame = 0; frame < 2; frame++)
                {
                    yield return null; // Native GameTime only; no graph/Animator drive.
                    foreach (var reader in readers) Advance(reader, "before-write-" + frame);
                }
                foreach (var reader in readers)
                {
                    Attach(reader); // Synchronous once-only swap; its boundary remains unqualified.
                    double started = Time.realtimeSinceStartupAsDouble;
                    int previousFrame = Time.frameCount;
                    for (int frame = 1; frame <= 2; frame++)
                    {
                        yield return null; // Exactly two direct natural frames, not a readiness poll.
                        BoundaryFrame(reader, "swap-pending", frame, started, previousFrame);
                        previousFrame = Time.frameCount;
                    }
                    QualifyBoundary(reader, false);
                }
                for (int frame = 0; frame < 2; frame++)
                {
                    yield return null;
                    foreach (var reader in readers) Advance(reader, "attached-" + frame);
                }
                // NORMAL cleanup requires its distinct boundary qualification; the original fallback is unchanged.
                for (int i = readers.Length - 1; i >= 0; i--)
                {
                    var reader = readers[i];
                    BeginNormalRestore(reader); // Synchronous once-only exact restoration, still pending.
                    double started = Time.realtimeSinceStartupAsDouble;
                    int previousFrame = Time.frameCount;
                    for (int frame = 1; frame <= 2; frame++)
                    {
                        yield return null; // No full guard retry or write during the blind interval.
                        BoundaryFrame(reader, "restoration-pending", frame, started, previousFrame);
                        previousFrame = Time.frameCount;
                    }
                    QualifyBoundary(reader, true);
                    Need(reader.restorationBoundaryQualified && reader.swapBoundaryQualified, "normal-cleanup-boundaries-not-qualified");
                    Need(Cleanup(reader), "cleanup-not-qualified");
                }
                for (int frame = 0; frame < 2; frame++)
                {
                    yield return null;
                    foreach (var reader in readers) Advance(reader, "restored-" + frame);
                }
                foreach (var reader in readers)
                {
                    Need(reader.swapBoundaryQualified && reader.restorationBoundaryQualified
                        && reader.restored && !reader.owned.IsValid(), "positive-restoration-missing");
                    reader.complete = true; reader.stage = "attachment-restoration-only";
                }
            }
            finally
            {
                // Never release a borrowed Animator/visual/graph. Existing fixture teardown remains byte-exact.
                for (int i = readers.Length - 1; i >= 0; i--)
                {
                    var reader = readers[i]; if (reader == null) continue;
                    if (reader.createAttempt && !reader.restored && !reader.cleanupBlocked) Cleanup(reader);
                    reader.Finish(i);
                }
            }
        }
        private static void Capture(Reader reader, int index, string provenance)
        {
            Need(reader.body != null, "body-null");
            var capture = typeof(CharacterPresentation).GetMethod("TryGetFarewellBody", Flags);
            reader.holds = typeof(CharacterPresentation).GetMethod("HoldsFarewellBody", Flags);
            Need(capture != null && reader.holds != null, "body-identity-api");
            var args = new object[] { null };
            Need((bool)capture.Invoke(reader.body, args) && args[0] != null, "actual-provided-body");
            reader.identity = args[0]; reader.animator = (Animator)Field(reader, "Animator");
            reader.controller = (RuntimeAnimatorController)Field(reader, "Controller");
            var handle = Resources.Load<AnimatorOverrideController>("Animation/GamesimHumanoid");
            reader.authority = handle != null ? handle.runtimeAnimatorController : null;
            Need(reader.animator != null && reader.controller != null && reader.authority != null, "actual-body-authority");
            reader.culling = reader.animator.cullingMode;
            var overrides = reader.controller as AnimatorOverrideController;
            Need(reader.controller == reader.authority || overrides != null && overrides.runtimeAnimatorController == reader.authority, "resources-base-lineage");
            reader.root = reader.body.transform.position; reader.heading = reader.body.transform.rotation;
            reader.frame = reader.lastFrame = Time.frameCount;
            Need(Finite(reader.root) && Finite(reader.heading), "initial-root-heading");
            reader.phase = reader.lastPhase = Selection(reader, "capture");
            reader.graph = reader.animator.playableGraph;
            reader.baseline = Inventory(reader);
            Need(reader.baseline.nodes.Count < Nodes && reader.baseline.roots.Count < Roots && reader.baseline.edges <= Edges - 2, "owned-node-inventory-reserve");
            foreach (var output in reader.baseline.outputs)
            {
                if (output.type != typeof(UnityEngine.Animations.AnimationPlayableOutput) || output.target != reader.animator) continue;
                Need(reader.output == null, "multiple-captured-animator-outputs"); reader.output = output;
            }
            Need(reader.output != null && reader.output.source.IsValid() && reader.output.weight > 0f && reader.output.weight <= 1f, "unique-positive-existing-output");
            var source = Find(reader.baseline, reader.output.source);
            Need(source != null && reader.output.port >= 0 && reader.output.port < source.outputs.Length, "explicit-existing-source-port");
            Need(source.outputs[reader.output.port].IsNull() && !source.outputs[reader.output.port].IsValid(), "source-port-already-has-playable-consumer");
            Need(reader.baseline.roots.Contains(reader.output.source), "captured-source-not-observed-root");
            // The native observed seam is a root with no playable consumers, not permission to resize it.
            foreach (var destination in source.outputs) Need(destination.IsNull() && !destination.IsValid(), "source-has-another-playable-consumer");
            reader.Line("BEGIN body=" + index + " provenance=" + Bounded(provenance));
            reader.Line("IDENTITY character=" + Bounded(reader.body.CharacterId) + " frame=" + reader.body.Frame
                + " visual=" + Id((UnityEngine.Object)Field(reader, "Visual")) + " providedRoot=" + Id((UnityEngine.Object)Field(reader, "ProvidedRoot"))
                + " animator=" + Id(reader.animator) + " host=" + Id(reader.animator.gameObject) + " controller=" + Id(reader.controller)
                + " culling=" + reader.culling + " resourcesBase=" + Id(reader.authority) + " avatar=" + Id((UnityEngine.Object)Field(reader, "Avatar"))
                + " build=" + Id((UnityEngine.Object)Field(reader, "Build")) + " revision=" + Field(reader, "RequestRevision")
                + " clip=" + Id(reader.clip) + " clipName=" + Bounded(reader.clip.name) + " clipLength=" + Number(reader.clip.length));
            DescribeBaseline(reader);
            ObserveReached(reader, "captured");
        }
        private static float Selection(Reader reader, string boundary)
        {
            bool bodyHeld = reader.body != null && reader.animator != null && (bool)reader.holds.Invoke(reader.body, new[] { reader.identity });
            if (!bodyHeld) BodyRefusalFacts(reader, boundary);
            Need(bodyHeld, "captured-body-not-held");
            Need(reader.animator.runtimeAnimatorController == reader.controller && reader.animator.cullingMode == reader.culling, "controller-or-culling-replaced");
            var handle = Resources.Load<AnimatorOverrideController>("Animation/GamesimHumanoid");
            Need(handle != null && handle.runtimeAnimatorController == reader.authority, "resources-authority-replaced");
            Need(reader.body.TryReadFarewell(out var pose) && pose.Idle && !pose.InTake
                && pose.Boundary == CharacterPresentation.FarewellBoundary.None && !reader.animator.IsInTransition(0), "actual-positive-settled-idle");
            var state = reader.animator.GetCurrentAnimatorStateInfo(0);
            var clips = reader.animator.GetCurrentAnimatorClipInfo(0);
            Need(state.IsName("Idle") && clips.Length == 1 && clips[0].clip != null && Finite(clips[0].weight)
                && clips[0].weight > 0f && clips[0].weight <= 1f && clips[0].clip.humanMotion
                && Finite(clips[0].clip.length) && clips[0].clip.length > 0f
                && Finite(state.normalizedTime) && state.normalizedTime >= 0f, "selected-human-idle");
            if (reader.clip == null) { reader.clip = clips[0].clip; reader.state = state.fullPathHash; }
            Need(clips[0].clip == reader.clip && state.fullPathHash == reader.state, "selected-idle-identity-changed");
            Need(Finite(pose.Root) && Finite(pose.Heading) && Finite(pose.Head) && Finite(pose.LeftHand) && Finite(pose.RightHand)
                && Finite(pose.LeftFoot) && Finite(pose.RightFoot) && Finite(pose.LeftToes) && Finite(pose.RightToes), "pose-finite");
            Need(Vector3.Distance(reader.root, pose.Root) <= .001f && Quaternion.Angle(reader.heading, pose.Heading) <= .05f, "root-heading-drift");
            reader.Line("POSE boundary=" + Bounded(boundary) + " frame=" + Time.frameCount + " phase=" + Number(state.normalizedTime)
                + " selectedWeight=" + Number(clips[0].weight) + " root=" + Vector(pose.Root) + " heading=" + Quat(pose.Heading)
                + " head=" + Vector(pose.Head) + " leftHand=" + Vector(pose.LeftHand) + " rightHand=" + Vector(pose.RightHand)
                + " leftFoot=" + Vector(pose.LeftFoot) + " rightFoot=" + Vector(pose.RightFoot)
                + " leftToes=" + Vector(pose.LeftToes) + " rightToes=" + Vector(pose.RightToes)
                + " rootDrift=" + Number(Vector3.Distance(reader.root, pose.Root)) + " headingDrift=" + Number(Quaternion.Angle(reader.heading, pose.Heading))
                + " temporalFeetAreMeasurementsNotPlantedPass=true");
            return state.normalizedTime;
        }
        private static Snapshot Inventory(Reader reader)
        {
            var graph = reader.animator.playableGraph;
            Need(graph.Equals(reader.graph) && graph.IsValid(), "actual-graph-invalid-or-replaced");
            Need(graph.IsPlaying() && graph.GetTimeUpdateMode() == DirectorUpdateMode.GameTime, "native-playing-gametime");
            int count = graph.GetPlayableCount(), roots = graph.GetRootPlayableCount(), outputs = graph.GetOutputCount();
            Need(count > 0 && count <= Nodes && roots > 0 && roots <= Roots && outputs > 0 && outputs <= Outputs, "whole-graph-count-cap");
            var scan = new Snapshot();
            var known = new System.Collections.Generic.List<Playable>();
            for (int i = 0; i < roots; i++)
            {
                var root = graph.GetRootPlayable(i); Need(root.IsValid(), "invalid-root");
                Need(!scan.roots.Contains(root), "duplicate-root"); scan.roots.Add(root); Admit(reader, known, root);
            }
            for (int i = 0; i < outputs; i++)
            {
                var output = graph.GetOutput(i); Need(output.IsOutputValid() && !output.IsOutputNull(), "invalid-existing-output");
                var record = new Output { handle = output, type = output.GetPlayableOutputType(), source = output.GetSourcePlayable(),
                    port = output.GetSourceOutputPort(), weight = output.GetWeight(), reference = output.GetReferenceObject(), user = output.GetUserData() };
                Need(record.type != null && Finite(record.weight), "output-metadata");
                if (output.IsPlayableOutputOfType<UnityEngine.Animations.AnimationPlayableOutput>())
                    record.target = ((UnityEngine.Animations.AnimationPlayableOutput)output).GetTarget();
                foreach (var prior in scan.outputs) Need(!prior.handle.Equals(output), "duplicate-output");
                scan.outputs.Add(record); Admit(reader, known, record.source);
            }
            // Returned disconnected own node must be reachable as a root; never invent its membership by appending it.
            for (int cursor = 0; cursor < known.Count; cursor++)
            {
                var playable = known[cursor]; int inputs = playable.GetInputCount(), destinations = playable.GetOutputCount();
                Need(inputs >= 0 && inputs <= Edges && destinations >= 0 && destinations <= Edges, "node-port-cap");
                var record = new Node { handle = playable, type = playable.GetPlayableType(),
                    inputs = new Playable[inputs], outputs = new Playable[destinations], weights = new float[inputs] };
                Need(record.type != null, "node-type");
                for (int p = 0; p < inputs; p++)
                {
                    Need(++scan.edges <= Edges, "edge-cap"); record.inputs[p] = playable.GetInput(p); record.weights[p] = playable.GetInputWeight(p);
                    Need(Finite(record.weights[p]), "input-weight"); Admit(reader, known, record.inputs[p]);
                }
                for (int p = 0; p < destinations; p++)
                { Need(++scan.edges <= Edges, "edge-cap"); record.outputs[p] = playable.GetOutput(p); Admit(reader, known, record.outputs[p]); }
                scan.nodes.Add(record);
            }
            Need(scan.nodes.Count == count, "not-whole-public-graph");
            return scan;
        }
        private static void Admit(Reader reader, System.Collections.Generic.List<Playable> known, Playable value)
        {
            if (value.IsNull()) { Need(!value.IsValid(), "null-valid-sentinel"); return; }
            Need(value.IsValid() && value.GetGraph().Equals(reader.graph), "invalid-or-foreign-reachable-node");
            if (!known.Contains(value)) { Need(known.Count < Nodes, "visited-cap"); known.Add(value); }
        }
        private static Reached ObserveReached(Reader reader, string boundary)
        {
            reader.stage = boundary;
            Selection(reader, boundary);
            var scan = Inventory(reader);
            var result = new Reached { scan = scan, ownedValid = reader.returned && reader.owned.IsValid() };
            Need(!reader.returned || result.ownedValid || reader.destroyAttempt, "owned-invalid-before-own-destroy-attempt");
            Need(scan.nodes.Count == reader.baseline.nodes.Count + (result.ownedValid ? 1 : 0), "unexpected-node-membership");
            foreach (var original in reader.baseline.nodes)
            {
                var actual = Find(scan, original.handle); Need(actual != null && actual.type == original.type
                    && actual.inputs.Length == original.inputs.Length && actual.outputs.Length == original.outputs.Length, "original-node-shape-changed");
                for (int p = 0; p < original.inputs.Length; p++)
                    Need(actual.inputs[p].Equals(original.inputs[p]) && actual.weights[p].Equals(original.weights[p]), "original-input-or-weight-changed");
            }
            Node own = result.ownedValid ? Find(scan, reader.owned) : null;
            if (result.ownedValid)
            {
                Need(own != null && own.type == typeof(UnityEngine.Animations.AnimationScriptPlayable)
                    && own.inputs.Length == 1 && own.outputs.Length == 1 && reader.job.GetProcessInputs(), "returned-owned-node-contract");
                result.connected = own.inputs[0].Equals(reader.output.source);
                Need(result.connected || own.inputs[0].IsNull(), "foreign-owned-input");
                Need(own.weights[0].Equals(reader.createdWeight) || reader.weightAttempt && own.weights[0].Equals(1f), "foreign-owned-weight");
                foreach (var destination in own.outputs) Need(destination.IsNull() && !destination.IsValid(), "foreign-owned-playable-consumer");
            }
            foreach (var original in reader.baseline.nodes)
            {
                var actual = Find(scan, original.handle);
                for (int p = 0; p < original.outputs.Length; p++)
                {
                    var expected = result.connected && original.handle.Equals(reader.output.source) && p == reader.output.port ? reader.owned : original.outputs[p];
                    Need(actual.outputs[p].Equals(expected), "original-output-or-foreign-consumer-changed");
                }
            }
            Need(scan.outputs.Count == reader.baseline.outputs.Count, "graph-output-count-changed");
            for (int i = 0; i < reader.baseline.outputs.Count; i++)
            {
                var old = reader.baseline.outputs[i]; var current = scan.outputs[i];
                Need(current.handle.Equals(old.handle) && current.type == old.type && current.weight.Equals(old.weight)
                    && current.target == old.target && System.Object.ReferenceEquals(current.reference, old.reference)
                    && System.Object.ReferenceEquals(current.user, old.user), "original-output-identity-target-weight-changed");
                if (old.handle.Equals(reader.output.handle))
                {
                    result.swapped = result.ownedValid && current.source.Equals(reader.owned) && current.port == 0;
                    Need(result.swapped || current.source.Equals(old.source) && current.port == old.port, "output-foreign-source-or-port");
                }
                else Need(current.source.Equals(old.source) && current.port == old.port, "foreign-output-source-changed");
            }
            Need(!result.swapped || result.connected, "swapped-without-owned-connection");
            // Observed shape never creates mutation authority: require our once-only attempted stage first.
            Need(!result.connected || reader.connectAttempt, "unattempted-owned-input-adoption");
            Need(!result.swapped || reader.swapAttempt, "unattempted-output-adoption");
            Need(!reader.connectionReached || reader.disconnectAttempt || result.connected || !result.ownedValid,
                "connection-undone-before-own-disconnect-attempt");
            Need(!reader.swapReached || reader.restoreAttempt || result.swapped || !result.ownedValid,
                "output-undone-before-own-restore-attempt");
            Need(!reader.disconnectionReached || !result.connected, "connection-reappeared-after-own-disconnect");
            Need(!reader.outputRestorationReached || !result.swapped, "output-readopted-after-own-restoration");
            Need(!reader.weightReached || !result.ownedValid || own.weights[0].Equals(1f), "owned-weight-undone");
            Need(!result.swapped || reader.weightReached, "swap-before-qualified-own-weight");
            var expectedRoots = new System.Collections.Generic.List<Playable>(reader.baseline.roots);
            if (result.ownedValid)
            {
                if (result.connected) Need(expectedRoots.Remove(reader.output.source), "source-root-not-captured");
                expectedRoots.Add(reader.owned);
            }
            Need(scan.roots.Count == expectedRoots.Count && scan.roots.All(expectedRoots.Contains), "unexpected-root-membership");
            if (!result.ownedValid)
                Need(scan.roots.SequenceEqual(reader.baseline.roots), "restored-root-order-changed");
            foreach (var node in scan.nodes) Need(Find(reader.baseline, node.handle) != null || result.ownedValid && node.handle.Equals(reader.owned), "unexpected-node");
            // Latch only after complete identity/graph/root/port/weight comparisons have succeeded.
            // This also classifies partial effects after our attempted throwing/false operation, without retry.
            if (result.connected) reader.connectionReached = true;
            if (result.swapped) reader.swapReached = true;
            if (reader.weightAttempt && own != null && own.weights[0].Equals(1f)) reader.weightReached = true;
            if (reader.restoreAttempt && !result.swapped) reader.outputRestorationReached = true;
            if (reader.disconnectAttempt && !result.connected) reader.disconnectionReached = true;
            reader.Line("REACHED boundary=" + Bounded(boundary) + " nodes=" + scan.nodes.Count + " roots=" + scan.roots.Count
                + " outputs=" + scan.outputs.Count + " portReads=" + scan.edges + " ownedValid=" + result.ownedValid
                + " connected=" + result.connected + " swapped=" + result.swapped + " connectionReached=" + reader.connectionReached
                + " swapReached=" + reader.swapReached + " ownOutputRestorationReached=" + reader.outputRestorationReached
                + " ownDisconnectionReached=" + reader.disconnectionReached + " originalKnownTopologyHeld=true"
                + " originalOppositePorts=opaquePublicApi otherConsumersAbsent=true"
                + " originalSourcePort=" + reader.output.port + " newInputWeight="
                + (own != null ? Number(own.weights[0]) : "unavailable") + " processInputs=" + (own != null ? "true" : "unavailable"));
            return result;
        }
        private static void Advance(Reader reader, string boundary)
        {
            ObserveReached(reader, boundary); float phase = reader.animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            Need(Time.frameCount > reader.lastFrame && Finite(phase) && phase > reader.lastPhase, "natural-idle-did-not-advance");
            reader.Line("ADVANCE boundary=" + Bounded(boundary) + " previousFrame=" + reader.lastFrame + " currentFrame=" + Time.frameCount
                + " previousPhase=" + Number(reader.lastPhase) + " currentPhase=" + Number(phase) + " nativeIdleAdvanced=true");
            reader.lastFrame = Time.frameCount; reader.lastPhase = phase;
        }
        private static void Attach(Reader reader)
        {
            Need(!reader.createAttempt && !ObserveReached(reader, "before-create").ownedValid, "create-not-owned-once");
            reader.stage = "create-attempt"; reader.Line("ATTEMPT operation=Create before=true"); reader.createAttempt = true;
            reader.job = UnityEngine.Animations.AnimationScriptPlayable.Create(reader.graph, new EmptyJob(), 1);
            reader.owned = reader.job; reader.returned = reader.owned.IsValid();
            Need(reader.returned, "create-returned-no-valid-handle");
            reader.containment = "captured-owned-handle";
            // This is the returned node's initial weight observation, not a write or foreign-node lookup.
            reader.createdWeight = reader.owned.GetInputWeight(0);
            Need(Finite(reader.createdWeight) && reader.createdWeight >= 0f && reader.createdWeight <= 1f, "created-input-weight");
            var created = ObserveReached(reader, "after-create");
            Need(!created.connected && !created.swapped, "create-unexpected-connection");
            var beforeConnect = ObserveReached(reader, "before-connect");
            Need(!beforeConnect.connected && !beforeConnect.swapped, "before-connect-unexpected-adoption");
            reader.stage = "connect-attempt"; reader.Line("ATTEMPT operation=Connect sourcePort=" + reader.output.port + " inputPort=0 before=true");
            Need(!reader.connectAttempt, "connect-retry-forbidden"); reader.connectAttempt = true;
            bool connected = reader.graph.Connect(reader.output.source, reader.output.port, reader.job, 0);
            var connection = ObserveReached(reader, "after-connect");
            Need(connected && connection.connected && !connection.swapped, "connect-not-exact");
            var beforeWeight = ObserveReached(reader, "before-new-weight");
            Need(beforeWeight.connected && !beforeWeight.swapped && reader.connectionReached, "before-weight-stage-not-held");
            reader.stage = "new-weight-attempt"; reader.Line("ATTEMPT operation=SetOwnedInputWeight inputPort=0 value=1 before=true");
            Need(!reader.weightAttempt, "weight-retry-forbidden"); reader.weightAttempt = true; reader.owned.SetInputWeight(0, 1f);
            var weighted = ObserveReached(reader, "after-new-weight");
            Need(weighted.connected && Find(weighted.scan, reader.owned).weights[0].Equals(1f), "owned-input-weight-not-one");
            var beforeSwap = ObserveReached(reader, "before-swap");
            Need(beforeSwap.connected && !beforeSwap.swapped && reader.connectionReached && reader.weightReached, "before-swap-stage-not-held");
            reader.stage = "swap-attempt"; reader.Line("ATTEMPT operation=SetCapturedOutputSource ownedPort=0 before=true");
            Need(!reader.swapAttempt, "swap-retry-forbidden"); reader.swapAttempt = true; reader.output.handle.SetSourcePlayable(reader.job, 0);
            reader.stage = "swap-pending"; // No immediate post-setter readiness assumption or success latch.
        }
        private static void BoundaryFrame(Reader reader, string boundary, int ordinal, double started, int previousFrame)
        {
            double elapsed = Time.realtimeSinceStartupAsDouble - started;
            Need(!double.IsNaN(started) && !double.IsInfinity(started)
                && !double.IsNaN(elapsed) && !double.IsInfinity(elapsed) && elapsed >= 0.0 && elapsed <= 5.0,
                "natural-boundary-real-time-cap");
            Need(Time.frameCount > previousFrame, "natural-boundary-frame-not-later");
            reader.Line("PENDING boundary=" + Bounded(boundary) + " ordinal=" + ordinal
                + " previousFrame=" + previousFrame + " currentFrame=" + Time.frameCount
                + " elapsedSeconds=" + elapsed.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + " elapsedCapSeconds=5 boundaryQualified=false noGuardRetry=true continuousOwnershipQualified=false");
        }
        private static void QualifyBoundary(Reader reader, bool restoration)
        {
            string boundary = restoration ? "normal-restoration-boundary" : "swap-boundary";
            Advance(reader, boundary); // Complete original full guard/topology AND monotonic phase, raw-exact.
            var reached = ObserveReached(reader, boundary + "-shape"); // One further successful-path endpoint scan.
            var own = Find(reached.scan, reader.owned);
            Need(!reader.cleanupBlocked && reader.returned && reader.createAttempt && reader.connectAttempt
                && reader.weightAttempt && reader.swapAttempt && !reader.disconnectAttempt && !reader.destroyAttempt
                && reached.ownedValid && reached.connected && reader.connectionReached && reader.weightReached
                && own != null && own.inputs[0].Equals(reader.output.source) && own.weights[0].Equals(1f),
                "natural-boundary-owned-shape-not-held");
            if (restoration)
            {
                Need(reader.swapBoundaryQualified && !reader.restorationBoundaryQualified && reader.restoreAttempt
                    && !reached.swapped && reader.outputRestorationReached, "normal-restoration-shape-not-exact");
                reader.Line("BOUNDARY restorationQualified=true swapQualified=true fullAdvanceAndEndpointShape=true continuousOwnershipQualified=false");
                reader.restorationBoundaryQualified = true;
            }
            else
            {
                Need(!reader.swapBoundaryQualified && !reader.restorationBoundaryQualified && !reader.restoreAttempt
                    && reached.swapped && reader.swapReached, "swap-boundary-shape-not-exact");
                reader.Line("BOUNDARY swapQualified=true restorationQualified=false fullAdvanceAndEndpointShape=true continuousOwnershipQualified=false");
                reader.swapBoundaryQualified = true;
            }
        }
        private static void BeginNormalRestore(Reader reader)
        {
            Need(reader.swapBoundaryQualified && !reader.restorationBoundaryQualified && !reader.cleanupBlocked
                && !reader.restoreAttempt && !reader.disconnectAttempt && !reader.destroyAttempt, "normal-restore-not-owned-once");
            var reached = ObserveReached(reader, "before-normal-restore");
            var own = Find(reached.scan, reader.owned);
            Need(reached.ownedValid && reached.connected && reached.swapped && reader.connectionReached && reader.weightReached
                && own != null && own.weights[0].Equals(1f), "normal-restore-stage-not-held");
            reader.stage = "restore-attempt"; reader.Line("ATTEMPT operation=RestoreCapturedOutput sourcePort=" + reader.output.port + " before=true");
            reader.restoreAttempt = true; reader.output.handle.SetSourcePlayable(reader.output.source, reader.output.port);
            reader.stage = "restoration-pending"; // No immediate post-setter readiness assumption or success latch.
        }
        private static bool Cleanup(Reader reader)
        {
            if (reader.restored) return true;
            if (reader.cleanupBlocked) return false;
            if (!reader.returned)
            {
                reader.stage = "create-without-returned-handle"; reader.containment = "FAILED_UNQUALIFIED-original-fixture-teardown-only";
                reader.cleanupBlocked = true; ContainmentFacts(reader); return false; // No count-delta recovery or borrowed graph destruction.
            }
            try
            {
                var reached = ObserveReached(reader, "cleanup-reached");
                Need(reached.ownedValid, "owned-node-invalid-before-qualified-destroy");
                if (reached.swapped)
                {
                    Need(!reader.restoreAttempt, "restore-retry-forbidden");
                    ObserveReached(reader, "before-restore");
                    reader.stage = "restore-attempt"; reader.Line("ATTEMPT operation=RestoreCapturedOutput sourcePort=" + reader.output.port + " before=true");
                    reader.restoreAttempt = true; reader.output.handle.SetSourcePlayable(reader.output.source, reader.output.port);
                    reached = ObserveReached(reader, "after-restore"); Need(!reached.swapped, "restore-not-exact");
                }
                if (reached.connected)
                {
                    Need(!reader.disconnectAttempt, "disconnect-retry-forbidden");
                    var before = ObserveReached(reader, "before-disconnect"); Need(!before.swapped && before.connected, "disconnect-ownership");
                    reader.stage = "disconnect-attempt"; reader.Line("ATTEMPT operation=DisconnectOwnedInput inputPort=0 before=true");
                    reader.disconnectAttempt = true; reader.graph.Disconnect(reader.job, 0);
                    reached = ObserveReached(reader, "after-disconnect"); Need(!reached.connected && !reached.swapped, "disconnect-not-exact");
                }
                Need(!reader.destroyAttempt, "destroy-retry-forbidden");
                var unconnected = ObserveReached(reader, "before-destroy");
                Need(unconnected.ownedValid && !unconnected.connected && !unconnected.swapped, "destroy-unqualified-or-consumed");
                reader.stage = "destroy-attempt"; reader.Line("ATTEMPT operation=DestroyOnlyReturnedOwnedNode before=true");
                reader.destroyAttempt = true; reader.graph.DestroyPlayable(reader.job);
                var restored = ObserveReached(reader, "after-destroy");
                Need(!restored.ownedValid && !reader.owned.IsValid(), "returned-node-not-invalid-after-destroy");
                reader.restored = true; reader.containment = "exact-observed-restoration"; ContainmentFacts(reader);
                return true;
            }
            catch (System.Exception error)
            {
                // Any throw/refusal stops further writes. Do not retry or infer rollback/reclamation.
                reader.cleanupBlocked = true; reader.containment = "FAILED_UNQUALIFIED-original-fixture-teardown-only";
                reader.cleanupFailure = error.GetType().Name.Length <= 128 ? error.GetType().Name : "type-not-bounded";
                if (error is System.InvalidOperationException && error.Message != null && error.Message.StartsWith("inert-attachment:", System.StringComparison.Ordinal)
                    && error.Message.Length <= Text && !error.Message.Any(char.IsControl)) reader.cleanupRefusal = error.Message;
                else reader.cleanupRefusal = "native-or-unbounded-refusal";
                ContainmentFacts(reader); // Independent read-only validity facts; no assumed rollback or immediate reclamation.
                return false;
            }
        }
        private readonly struct BodySample<T>
        {
            internal readonly bool Observed;
            internal readonly T Value;
            internal BodySample(T value) { Observed = true; Value = value; }
        }
        private static BodySample<T> BodyRead<T>(bool allowed, System.Func<T> read)
        {
            if (!allowed) return default;
            try { return new BodySample<T>(read()); }
            catch (System.Exception) { return default; } // A failed diagnostic read is unobserved, never a guard waiver.
        }
        private static string BodyFact<T>(BodySample<T> sample, System.Func<T, string> format)
        {
            if (!sample.Observed) return "unobserved";
            try
            {
                string value = format(sample.Value);
                return value != null && value.Length <= Text && !value.Any(char.IsControl) ? value : "unobserved";
            }
            catch (System.Exception) { return "unobserved"; }
        }
        private static object BodyField(Reader reader, string name)
        {
            var field = typeof(CharacterPresentation).GetField(name, Flags);
            if (field == null) throw new System.InvalidOperationException();
            return field.GetValue(reader.body); // Read only the same production owner's field; no private method/graph invocation.
        }
        private static void BodyRefusalFacts(Reader reader, string boundary, bool afterFailedCleanup = false,
            int frameOrdinal = 0, int previousFrame = 0)
        {
            if (!afterFailedCleanup)
            {
                if (reader.bodyRefusalRecorded) return;
                reader.bodyRefusalRecorded = true;
            }
            try
            {
                bool bodyPresent = reader.body != null;
                var built = BodyRead(bodyPresent, () => (bool)BodyField(reader, "built"));
                var provided = BodyRead(bodyPresent, () => (CharacterBody)BodyField(reader, "providedBody"));
                var visual = BodyRead(bodyPresent, () => (Transform)BodyField(reader, "visual"));
                var cached = BodyRead(bodyPresent, () => (Animator)BodyField(reader, "animator"));
                var assembling = BodyRead(bodyPresent, () => reader.body.IsBodyAssembling);
                var changing = BodyRead(bodyPresent, () => reader.body.IsChangingOutfit);
                var exists = BodyRead(provided.Observed, () => provided.Value.Exists);
                var deferred = BodyRead(provided.Observed, () => provided.Value.Deferred);
                var root = BodyRead(provided.Observed, () => provided.Value.Root);
                bool rootPresent = root.Observed && root.Value != null;
                var live = BodyRead(rootPresent, () => root.Value.GetComponentInChildren<Animator>(true));
                var build = BodyRead(rootPresent, () => root.Value.GetComponent<CharacterBodyBuildState>());
                bool livePresent = live.Observed && live.Value != null;
                bool buildPresent = build.Observed && build.Value != null;
                var active = BodyRead(livePresent, () => live.Value.isActiveAndEnabled);
                var initialized = BodyRead(livePresent, () => live.Value.isInitialized);
                var human = BodyRead(livePresent, () => live.Value.isHuman);
                var avatar = BodyRead(livePresent, () => live.Value.avatar);
                var avatarValid = BodyRead(avatar.Observed && avatar.Value != null, () => avatar.Value.isValid);
                var controller = BodyRead(livePresent, () => live.Value.runtimeAnimatorController);
                var rootMotion = BodyRead(livePresent, () => live.Value.applyRootMotion);
                var speed = BodyRead(livePresent, () => live.Value.speed);
                var updateMode = BodyRead(livePresent, () => live.Value.updateMode);
                var ready = BodyRead(buildPresent, () => build.Value.Ready);
                var revision = BodyRead(build.Observed, () => build.Value != null ? build.Value.Revision : 0);
                bool captured = reader.identity != null;
                var text = new System.Text.StringBuilder(afterFailedCleanup
                    ? "BODY_AFTER_REFUSAL sampledAfterFailedCleanup=true aggregateHeld=unobserved guardReexecuted=false notCleanupAuthority=true atomicSnapshot=false"
                    : "BODY_REFUSAL aggregateHeld=false factsSampledAfterGuard=true failingBranchUnproven=true guardReexecuted=false")
                    .Append(" boundary=").Append(Bounded(boundary)).Append(" frame=").Append(Time.frameCount)
                    .Append(" bodyPresent=").Append(bodyPresent).Append(" capturedAnimatorPresent=").Append(reader.animator != null)
                    .Append(" built=").Append(BodyFact(built, x => x.ToString()))
                    .Append(" providedExists=").Append(BodyFact(exists, x => x.ToString()))
                    .Append(" assembling=").Append(BodyFact(assembling, x => x.ToString()))
                    .Append(" changingOutfit=").Append(BodyFact(changing, x => x.ToString()))
                    .Append(" visualPresent=").Append(BodyFact(BodyRead(visual.Observed, () => visual.Value != null), x => x.ToString()))
                    .Append(" providedDeferred=").Append(BodyFact(deferred, x => x.ToString()))
                    .Append(" providedRootPresent=").Append(BodyFact(BodyRead(root.Observed, () => root.Value != null), x => x.ToString()))
                    .Append(" liveAnimatorPresent=").Append(BodyFact(BodyRead(live.Observed, () => live.Value != null), x => x.ToString()))
                    .Append(" liveMatchesCached=").Append(BodyFact(BodyRead(live.Observed && cached.Observed, () => live.Value == cached.Value), x => x.ToString()))
                    .Append(" activeEnabled=").Append(BodyFact(active, x => x.ToString()))
                    .Append(" initialized=").Append(BodyFact(initialized, x => x.ToString()))
                    .Append(" human=").Append(BodyFact(human, x => x.ToString()))
                    .Append(" avatarPresent=").Append(BodyFact(BodyRead(avatar.Observed, () => avatar.Value != null), x => x.ToString()))
                    .Append(" avatarValid=").Append(BodyFact(avatarValid, x => x.ToString()))
                    .Append(" controllerPresent=").Append(BodyFact(BodyRead(controller.Observed, () => controller.Value != null), x => x.ToString()))
                    .Append(" applyRootMotion=").Append(BodyFact(rootMotion, x => x.ToString()))
                    .Append(" speed=").Append(BodyFact(speed, Number))
                    .Append(" speedFinite=").Append(BodyFact(BodyRead(speed.Observed, () => Finite(speed.Value)), x => x.ToString()))
                    .Append(" speedEqualsOne=").Append(BodyFact(BodyRead(speed.Observed, () => speed.Value == 1f), x => x.ToString()))
                    .Append(" updateMode=").Append(BodyFact(updateMode, x => ((int)x).ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .Append(" updateModeNormal=").Append(BodyFact(BodyRead(updateMode.Observed, () => updateMode.Value == AnimatorUpdateMode.Normal), x => x.ToString()))
                    .Append(" buildPresent=").Append(BodyFact(BodyRead(build.Observed, () => build.Value != null), x => x.ToString()))
                    .Append(" buildReady=").Append(BodyFact(ready, x => x.ToString()))
                    .Append(" buildAbsentOrReady=").Append(BodyFact(BodyRead(build.Observed && (!buildPresent || ready.Observed), () => !buildPresent || ready.Value), x => x.ToString()))
                    .Append(" deferredHasBuild=").Append(BodyFact(BodyRead(deferred.Observed && build.Observed, () => !deferred.Value || build.Value != null), x => x.ToString()))
                    .Append(" requestRevision=").Append(BodyFact(revision, x => x.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .Append(" visualMatchesCaptured=").Append(BodyFact(BodyRead(captured && visual.Observed, () => visual.Value == (Transform)Field(reader, "Visual")), x => x.ToString()))
                    .Append(" rootMatchesCaptured=").Append(BodyFact(BodyRead(captured && root.Observed, () => root.Value == (GameObject)Field(reader, "ProvidedRoot")), x => x.ToString()))
                    .Append(" animatorMatchesCaptured=").Append(BodyFact(BodyRead(captured && live.Observed, () => live.Value == (Animator)Field(reader, "Animator")), x => x.ToString()))
                    .Append(" controllerMatchesCaptured=").Append(BodyFact(BodyRead(captured && controller.Observed, () => controller.Value == (RuntimeAnimatorController)Field(reader, "Controller")), x => x.ToString()))
                    .Append(" avatarMatchesCaptured=").Append(BodyFact(BodyRead(captured && avatar.Observed, () => avatar.Value == (Avatar)Field(reader, "Avatar")), x => x.ToString()))
                    .Append(" buildMatchesCaptured=").Append(BodyFact(BodyRead(captured && build.Observed, () => build.Value == (CharacterBodyBuildState)Field(reader, "Build")), x => x.ToString()))
                    .Append(" revisionMatchesCaptured=").Append(BodyFact(BodyRead(captured && revision.Observed, () => revision.Value == (int)Field(reader, "RequestRevision")), x => x.ToString()))
                    .Append(" currentVisual=").Append(BodyFact(visual, x => Id(x)))
                    .Append(" currentRoot=").Append(BodyFact(root, x => Id(x)))
                    .Append(" cachedAnimator=").Append(BodyFact(cached, x => Id(x)))
                    .Append(" liveAnimator=").Append(BodyFact(live, x => Id(x)))
                    .Append(" currentAvatar=").Append(BodyFact(avatar, x => Id(x)))
                    .Append(" currentController=").Append(BodyFact(controller, x => Id(x)))
                    .Append(" currentBuild=").Append(BodyFact(build, x => Id(x)))
                    .Append(" noGraphReadsOrWrites=true diagnosticNotCleanupAuthority=true");
                if (afterFailedCleanup)
                {
                    var position = BodyRead(bodyPresent, () => reader.body.transform.position);
                    var rotation = BodyRead(bodyPresent, () => reader.body.transform.rotation);
                    text.Append(" frameOrdinal=").Append(frameOrdinal).Append(" previousFrame=").Append(previousFrame)
                        .Append(" strictlyLaterFrame=").Append(Time.frameCount > previousFrame)
                        .Append(" rootDrift=").Append(BodyFact(position, x => Number(Vector3.Distance(reader.root, x))))
                        .Append(" headingDrift=").Append(BodyFact(rotation, x => Number(Quaternion.Angle(reader.heading, x))))
                        .Append(" cleanupStillBlocked=").Append(reader.cleanupBlocked)
                        .Append(" laterFactsCannotReleaseCleanup=true");
                }
                EmitBodyRefusal(reader, text.ToString(), afterFailedCleanup);
            }
            catch (System.Exception)
            {
                EmitBodyRefusal(reader, afterFailedCleanup
                    ? "BODY_AFTER_REFUSAL facts=unobserved notCleanupAuthority=true atomicSnapshot=false"
                    : "BODY_REFUSAL aggregateHeld=false facts=unobserved diagnosticNotCleanupAuthority=true", afterFailedCleanup);
            }
        }
        private static void EmitBodyRefusal(Reader reader, string line, bool afterFailedCleanup = false)
        {
            // This observation cannot replace the original refusal, even if formatting or the report cap fails.
            try
            {
                int size = System.Text.Encoding.UTF8.GetByteCount(line + "\n");
                if (size <= 4096 && reader.bytes + size <= Bytes - Tail) reader.Line(line);
                else
                {
                    string unavailable = afterFailedCleanup
                        ? "BODY_AFTER_REFUSAL facts=unobserved-cap notCleanupAuthority=true atomicSnapshot=false"
                        : "BODY_REFUSAL aggregateHeld=false facts=unobserved-cap diagnosticNotCleanupAuthority=true";
                    int fallbackSize = System.Text.Encoding.UTF8.GetByteCount(unavailable + "\n");
                    if (reader.bytes + fallbackSize <= Bytes - Tail) reader.Line(unavailable);
                }
            }
            catch (System.Exception) { } // Neither the original Need(false) nor the captured original exception is replaced.
        }
        private static void ContainmentFacts(Reader reader)
        {
            // Each finite fact may remain unobserved if that read itself throws. Never a native cleanup authority.
            try { reader.retainedGraphValid = reader.graph.IsValid(); } catch (System.Exception) { reader.retainedGraphValid = null; }
            try { reader.retainedOwnedValid = reader.returned ? (bool?)reader.owned.IsValid() : null; } catch (System.Exception) { reader.retainedOwnedValid = null; }
            try
            {
                reader.retainedBodyHeld = reader.body != null && reader.identity != null && reader.holds != null
                    ? (bool?)reader.holds.Invoke(reader.body, new[] { reader.identity }) : null;
            }
            catch (System.Exception) { reader.retainedBodyHeld = null; }
        }
        private static string Fact(bool? value) => value.HasValue ? (value.Value ? "true" : "false") : "unobserved";
        private static void DescribeBaseline(Reader reader)
        {
            reader.Line("BASELINE nodes=" + reader.baseline.nodes.Count + " roots=" + reader.baseline.roots.Count
                + " outputs=" + reader.baseline.outputs.Count + " portReads=" + reader.baseline.edges
                + " graphPlaying=true mode=GameTime completePublicInventory=true controllerObjectMatch=opaquePublicApi");
            for (int i = 0; i < reader.baseline.roots.Count; i++)
                reader.Line("BASE_ROOT index=" + i + " node=" + Index(reader.baseline, reader.baseline.roots[i]));
            for (int i = 0; i < reader.baseline.outputs.Count; i++)
            {
                var output = reader.baseline.outputs[i];
                reader.Line("BASE_OUTPUT index=" + i + " type=" + Bounded(output.type.FullName) + " sourceNode=" + Index(reader.baseline, output.source)
                    + " sourceOutputPort=" + output.port + " weight=" + Number(output.weight) + " target=" + Id(output.target)
                    + " reference=" + Id(output.reference) + " user=" + Id(output.user) + " captured=" + output.handle.Equals(reader.output.handle));
            }
            for (int i = 0; i < reader.baseline.nodes.Count; i++)
            {
                var node = reader.baseline.nodes[i];
                reader.Line("BASE_NODE identity=" + i + " type=" + Bounded(node.type.FullName) + " inputCount=" + node.inputs.Length + " outputCount=" + node.outputs.Length);
                for (int p = 0; p < node.inputs.Length; p++)
                    reader.Line("BASE_INPUT destination=" + i + " inputPort=" + p + " source=" + Index(reader.baseline, node.inputs[p])
                        + " weight=" + Number(node.weights[p]) + " oppositeSourcePort=opaquePublicApi");
                for (int p = 0; p < node.outputs.Length; p++)
                    reader.Line("BASE_DESTINATION source=" + i + " outputPort=" + p + " destination=" + Index(reader.baseline, node.outputs[p])
                        + " oppositeInputPort=opaquePublicApi");
            }
        }
        private static int Index(Snapshot scan, Playable handle)
        { for (int i = 0; i < scan.nodes.Count; i++) if (scan.nodes[i].handle.Equals(handle)) return i; return -1; }
        private static Node Find(Snapshot scan, Playable handle)
        { int index = Index(scan, handle); return index >= 0 ? scan.nodes[index] : null; }
        private static object Field(Reader reader, string name)
        { var field = reader.identity.GetType().GetField(name, Flags); Need(field != null, "identity-field"); return field.GetValue(reader.identity); }
        private static string Bounded(string value)
        { Need(value != null && value.Length <= Text && !value.Any(char.IsControl), "text-cap"); return value; }
        private static string Id(UnityEngine.Object value) => value != null ? value.GetEntityId().ToString() : "null";
        private static string Number(float value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        private static string Vector(Vector3 value) => Number(value.x) + "," + Number(value.y) + "," + Number(value.z);
        private static string Quat(Quaternion value) => Number(value.x) + "," + Number(value.y) + "," + Number(value.z) + "," + Number(value.w);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w)
            && value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w > 1e-8f;
        private static void Need(bool value, string code) { if (!value) throw new System.InvalidOperationException("inert-attachment:" + code); }
    }

    /// <summary>TEST ONLY. One frozen train/two holdouts, then a separately anchored strict trial. Not a repair or production admission.</summary>
    [DefaultExecutionOrder(200)]
    internal sealed class GroundedFarewellCalibratedIkProbe : MonoBehaviour
    {
        private const float WarmSeconds = .5f, CalibrationSeconds = .5f, SettleSeconds = .5f, TakeSeconds = 2f, ReleaseSeconds = .5f;
        private const float TotalSeconds = 4.5f, WarmLandmarkBound = .05f, GoalBound = .01f, OffsetBound = .008f;
        private const double CalibrationAngle = 4d, GoalAngleBound = 5d;
        private const float FootBound = .001f, RootBound = .001f, HeadingBound = .05f;
        private const int CallbackCap = 32768, HostCap = 16;
        private static readonly HumanBodyBones[] BoneNames =
            { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes };
        private static readonly AvatarIKGoal[] Goals = { AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot };
        private const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

        private enum Phase { Reference, Calibration, CalibrationRelease, Settle, Trial, TrialRelease, Finished }
        private sealed class ReleaseState
        {
            internal bool attempted, written;
            internal int laterZeroFrames, frame = -1, hash;
            internal float nativePhase;
            internal string refusal;
        }
        private sealed class Session
        {
            internal readonly GroundedFarewellCalibratedIkProbe[] readers = new GroundedFarewellCalibratedIkProbe[2];
            internal readonly System.Text.StringBuilder text = new System.Text.StringBuilder(4096);
            internal Phase phase = Phase.Reference;
            internal bool started, success, finalizing;
            internal int sample, enteredPhases;
            internal float startedAt, warmAt, calAt, calReleaseAt, settleAt, takeAt, releaseAt;
            internal string failure, diagnosticFailure, firstFailureRow;
            private int bytes, rows;
            private const int ByteCap = 4 * 1024 * 1024, RowCap = 1024, Reserve = 4096;
            internal bool Playing => phase == Phase.Trial;
            internal bool Releasing => phase == Phase.CalibrationRelease || phase == Phase.TrialRelease;
            internal float PhaseAt => phase == Phase.Reference ? warmAt : phase == Phase.Calibration ? calAt
                : phase == Phase.CalibrationRelease ? calReleaseAt : phase == Phase.Settle ? settleAt
                : phase == Phase.Trial ? takeAt : releaseAt;
            internal float PhaseLimit => phase == Phase.Calibration ? CalibrationSeconds : phase == Phase.Settle ? SettleSeconds
                : phase == Phase.Trial ? TakeSeconds : phase == Phase.Reference ? WarmSeconds : ReleaseSeconds;
            internal void Start()
            {
                Need(!started, "session-start-once"); started = true;
                startedAt = warmAt = Time.realtimeSinceStartup; enteredPhases = 1 << (int)Phase.Reference;
            }
            internal void Deadline()
            {
                float now = Time.realtimeSinceStartup, elapsed = now - PhaseAt, total = now - startedAt;
                Need(started && Finite(elapsed) && elapsed >= 0f && elapsed <= PhaseLimit
                    && Finite(total) && total >= 0f && total <= TotalSeconds, "original-phase-and-total-deadline");
            }
            internal void Move(Phase next)
            {
                if (failure == null) Deadline(); // Successful transitions cannot extend the preceding phase.
                Need(started && (enteredPhases & (1 << (int)next)) == 0, "phase-may-enter-only-once");
                float now = Time.realtimeSinceStartup;
                Need(Finite(now) && now >= startedAt && now - startedAt <= TotalSeconds, "total-deadline-before-phase");
                enteredPhases |= 1 << (int)next; phase = next;
                if (next == Phase.Calibration) calAt = now;
                else if (next == Phase.CalibrationRelease) calReleaseAt = now;
                else if (next == Phase.Settle) settleAt = now;
                else if (next == Phase.Trial) takeAt = now;
                else if (next == Phase.TrialRelease) releaseAt = now;
            }
            internal void Line(string line, bool row = false)
            {
                int size = System.Text.Encoding.UTF8.GetByteCount(line);
                Need((!row || rows < RowCap) && size <= ByteCap - Reserve - bytes, "report-cap");
                text.Append(line); bytes += size; if (row) rows++;
            }
            internal void Release()
            {
                if (Releasing || !started) return;
                if (phase == Phase.Trial) Move(Phase.TrialRelease);
                else if ((enteredPhases & (1 << (int)Phase.CalibrationRelease)) == 0) Move(Phase.CalibrationRelease);
                // Already released calibration ownership is not reacquired or zeroed after a settle failure.
            }
            internal void Fail(string stage)
            {
                if (failure == null) failure = stage;
                Release(); // failure is stamped BEFORE any later body's target callback.
            }
            internal void CalibrationBarrier()
            {
                if (failure != null || !readers.All(r => r.calSamples == sample + 1)) return;
                Need(readers[0].lateFrame == readers[1].lateFrame, "shared-calibration-same-evaluation-barrier");
                if (sample == 2) Move(Phase.CalibrationRelease); else sample++;
            }
            internal void Finish()
            {
                var tail = new System.Text.StringBuilder(1024);
                tail.Append("RESULT localFullWeightSiteFeasibilityPassed=").Append(success)
                    .Append(" failure=").Append(failure ?? "none").Append(" diagnosticFailure=").Append(diagnosticFailure ?? "none")
                    .Append(" baselinePolicy=first-qualifying-post-release-settled-Idle actualAllTrialBaselinesCaptured=")
                    .Append(readers.All(r => r != null && r.baselineReady))
                    .Append(" CtoAPlanting=false translationIdentified=false oldSource18Repair=false")
                    .Append(" production=false gameRoute=false fullSuite=false visual=false\n");
                foreach (var r in readers)
                {
                    if (r == null) { tail.Append("BODY UNREACHED\n"); continue; }
                    tail.Append("BODY ").Append(r.index).Append(" callbacks=").Append(r.callbacks).Append(" late=").Append(r.lateReads)
                        .Append(" setters=").Append(r.setterAttempts).Append(" calSamples=").Append(r.calSamples)
                        .Append(" A=").Append(r.baselineReady).Append(" entry=").Append(r.enteredFrame)
                        .Append(" positiveDemand=").Append(r.positiveTakeDemand).Append(" calRelease=")
                        .Append(r.calibrationRelease.attempted).Append('/').Append(r.calibrationRelease.written).Append('/').Append(r.calibrationRelease.laterZeroFrames)
                        .Append(" trialRelease=").Append(r.trialRelease.attempted).Append('/').Append(r.trialRelease.written).Append('/').Append(r.trialRelease.laterZeroFrames)
                        .Append(" owned=").Append(r.ownedMask).Append(" warmMax=").Append(N(r.maxWarm))
                        .Append(" CtoA=").Append(N(r.maxCtoA)).Append(" trialMax=").Append(N(r.maxFeet))
                        .Append(" maxRoot=").Append(N(r.maxRoot)).Append(" maxHeading=").Append(N(r.maxHeading)).Append('\n');
                }
                string value = (firstFailureRow ?? "") + tail;
                int size = System.Text.Encoding.UTF8.GetByteCount(value);
                Need(size <= Reserve && bytes + size <= ByteCap, "report-tail-cap"); text.Append(value);
            }
        }

        private CharacterPresentation body;
        private Animator live;
        private object identity;
        private System.Reflection.MethodInfo holds, foreign;
        private System.Reflection.PropertyInfo canBegin;
        private BodyFrame frame;
        private AnimatorCullingMode culling;
        private CharacterPresentation.Reaction? beforeReaction;
        private CharacterPresentation.Gesture? beforeGesture;
        private UnityEngine.SceneManagement.Scene scene;
        private string actor, animatorWitness, avatarWitness, revisionWitness;
        private AnimationClip idleClip, takeClip;
        private Session session;
        private int index, callbacks, lateReads, setterAttempts, callbackFrame = -1, lateFrame = -1, boundFrame;
        private int enteredFrame = -1, palmSamples, ownedMask, preMask, targetMask, calSamples;
        private int referenceFrame = -1, baselineFrame = -1, setterStampMask;
        private readonly ReleaseState calibrationRelease = new ReleaseState(), trialRelease = new ReleaseState();
        private ReleaseState ActiveRelease => callbackStage == Phase.CalibrationRelease ? calibrationRelease : trialRelease;
        private int beforeBoneMask, finalBoneMask, postWeightMask, postGoalMask;
        private bool selectionRead, callbackHandRead, lateHandRead;
        private Vector3 callbackRightHand, lateRightHand;
        private int modelPredictionMask, modelResidualMask;
        private bool pending, warmReady, baselineReady, cueFired, neutral, completed, demand, positiveTakeDemand;
        private Phase callbackStage;
        private int callbackSample = -1;
        private float lastIdlePhase;
        private readonly Vector3[] calibrationAnchors = new Vector3[4];
        private readonly Quaternion[] referenceGoalRotation = new Quaternion[2];
        private readonly Vector3[] frozenOffset = new Vector3[2], predictedFoot = new Vector3[2];
        private readonly Quaternion[] frozenRotation = new Quaternion[2], predictedFootRotation = new Quaternion[2];
        private readonly double[] positionResidual = new double[2], angleResidual = new double[2];
        private bool coefficientsFrozen;
        private float maxWarm, maxCtoA, callbackMaxWarm, callbackMaxRoot, callbackMaxHeading;
        private bool callbackRelease, callbackAfterRelease, callbackPlaying;
        private float timeScale, lastRow = float.NegativeInfinity, lastTakePhase = -1f, callbackPhase, initialIdlePhase;
        private float maxFeet, maxRoot, maxHeading, maxPrediction;
        private Vector3 rootAnchor;
        private Quaternion headingAnchor;
        private Vector3 preRoot, finalRoot;
        private Quaternion preHeading, finalHeading;
        private readonly struct NativeSelection
        {
            internal readonly CharacterPresentation.FarewellObservation pose;
            internal readonly int currentHash, nextHash;
            internal readonly float currentPhase, nextPhase, currentWeight, nextWeight;
            internal readonly bool transition;
            internal NativeSelection(CharacterPresentation.FarewellObservation value, AnimatorStateInfo current,
                AnimatorStateInfo next, bool crossing, float weight, float incomingWeight)
            { pose = value; currentHash = current.fullPathHash; nextHash = next.fullPathHash;
                currentPhase = current.normalizedTime; nextPhase = next.normalizedTime; transition = crossing;
                currentWeight = weight; nextWeight = incomingWeight; }
        }
        private NativeSelection callbackSelection;
        private readonly Transform[] bones = new Transform[4];
        private readonly Vector3[] anchors = new Vector3[4], pre = new Vector3[4], final = new Vector3[4];
        private readonly Quaternion[] baseRotation = new Quaternion[4], preRotation = new Quaternion[4], finalRotation = new Quaternion[4];
        private readonly Vector3[] goal = new Vector3[2], target = new Vector3[2], offset = new Vector3[2], predictedToe = new Vector3[2];
        private readonly Quaternion[] goalRotation = new Quaternion[2], targetRotation = new Quaternion[2], offsetRotation = new Quaternion[2];
        private readonly Vector3[] postGoal = new Vector3[2];
        private readonly Quaternion[] postGoalRotation = new Quaternion[2];
        private readonly float[] prePositionWeight = new float[2], preRotationWeight = new float[2];
        private readonly float[] postPositionWeight = new float[2], postRotationWeight = new float[2];
        private MonoBehaviour[] hosts;
        private bool[] enabledHosts, activeHosts;
        private readonly System.Collections.Generic.List<MonoBehaviour> currentHosts = new System.Collections.Generic.List<MonoBehaviour>(HostCap);
        private readonly System.Collections.Generic.List<AnimatorClipInfo> clips = new System.Collections.Generic.List<AnimatorClipInfo>(2);

        public static IEnumerator Observe(CharacterPresentation[] bodies, string provenance, bool requireUmaFrames)
        {
            var s = new Session();
            s.Line("CALIBRATED-IK-A version=1 provenance=" + provenance
                + " trainOnly=0 heldOut=1,2 holdoutDegrees=4 holdoutsIdentifyTranslation=false"
                + " trialBaseline=first-post-release-settled-Idle rootBaseline=original-bind"
                + " originalBounds=.001m/.05deg lateOrder=200 wholeFrameFinal=false production=false\n");
            try
            {
                BindPair(s, bodies, requireUmaFrames);
                if (s.failure == null) s.Start();
                while (s.failure == null && !s.readers.All(r => r.warmReady))
                { if (Time.realtimeSinceStartup - s.warmAt > WarmSeconds) s.Fail("missing-natural-reference"); else yield return null; }
                if (s.failure == null) s.Move(Phase.Calibration);
                while (s.failure == null && s.phase == Phase.Calibration)
                { if (Time.realtimeSinceStartup - s.calAt > CalibrationSeconds) s.Fail("calibration-three-pair-deadline"); else yield return null; }
                while (s.Releasing && !s.readers.All(r => r == null || r.calibrationRelease.written && r.calibrationRelease.laterZeroFrames == 2)
                    && Time.realtimeSinceStartup - s.calReleaseAt <= ReleaseSeconds) yield return null;
                if (s.failure == null && !s.readers.All(r => r.calibrationRelease.written && r.calibrationRelease.laterZeroFrames == 2
                    && r.ownedMask == 0)) s.Fail("calibration-release-unqualified");
                if (s.failure == null) s.Move(Phase.Settle);
                while (s.failure == null && !s.readers.All(r => r.baselineReady))
                { if (Time.realtimeSinceStartup - s.settleAt > SettleSeconds) s.Fail("first-post-release-Idle-missing"); else yield return null; }
                if (s.failure == null) Fire(s);
                while (s.failure == null && !s.readers.All(r => r.completed))
                { if (Time.realtimeSinceStartup - s.takeAt > TakeSeconds) s.Fail("natural-take-deadline"); else yield return null; }
                if (s.failure == null)
                {
                    if (!s.readers.All(r => r.demand && r.positiveTakeDemand)) s.Fail("missing-genuine-positive-take-demand");
                    else if (s.readers[0].enteredFrame != s.readers[1].enteredFrame) s.Fail("different-actual-entry-frames");
                    else s.Release();
                }
                if (s.phase == Phase.TrialRelease)
                {
                    while (!s.readers.All(r => r.trialRelease.written && r.trialRelease.laterZeroFrames == 2)
                        && Time.realtimeSinceStartup - s.releaseAt <= ReleaseSeconds) yield return null;
                    if (!s.readers.All(r => r.trialRelease.written && r.trialRelease.laterZeroFrames == 2)) s.Fail("trial-release-unqualified");
                }
                s.success = s.failure == null && s.readers.All(r => r.calSamples == 3 && r.coefficientsFrozen && r.baselineReady
                    && r.completed && r.demand && r.positiveTakeDemand && r.neutral && r.palmSamples > 0
                    && r.calibrationRelease.written && r.calibrationRelease.laterZeroFrames == 2
                    && r.trialRelease.written && r.trialRelease.laterZeroFrames == 2 && r.ownedMask == 0);
            }
            finally
            {
                // No off-callback setter, forced evaluation, target/bone restore, broad cancellation or cleanup retry.
                s.finalizing = true;
                foreach (var r in s.readers) if (r != null) r.enabled = false;
                try { s.Finish(); Debug.Log(s.text.ToString()); }
                finally { foreach (var r in s.readers) if (r != null) Object.Destroy(r); }
            }
            Assert.That(s.success, Is.True, "Calibrated local full-weight hypothesis: " + (s.failure ?? "completion missing"));
        }

        private static void BindPair(Session s, CharacterPresentation[] bodies, bool requireUmaFrames)
        {
            try
            {
                Need(bodies != null && bodies.Length == 2 && bodies[0] != bodies[1], "two-distinct-bodies");
                if (requireUmaFrames)
                    Need(bodies[0] != null && bodies[1] != null && bodies[0].Frame == BodyFrame.Masculine
                        && bodies[1].Frame == BodyFrame.Feminine, "actual-masculine-feminine-UMA-frames");
                for (int i = 0; i < 2; i++)
                {
                    var owner = bodies[i];
                    Need(owner != null && !owner.IsPrimitiveBody && owner.VisualRoot != null, "actual-provided-body");
                    var animator = owner.VisualRoot.GetComponentInChildren<Animator>(true);
                    Need(animator != null && animator.GetComponents<GroundedFarewellCalibratedIkProbe>().Length == 0, "fresh-actual-host");
                    s.readers[i] = animator.gameObject.AddComponent<GroundedFarewellCalibratedIkProbe>();
                    s.readers[i].Bind(owner, animator, i, s);
                }
            }
            catch (System.Exception error) { s.Fail(Code("bind", error)); }
        }

        private static void Fire(Session s)
        {
            try
            {
                foreach (var r in s.readers) { r.CheckIdentity(); Need((bool)r.canBegin.GetValue(r.body), "public-cue-preflight"); }
                Need(s.phase == Phase.Settle && s.readers.All(r => r.baselineReady), "sole-baseline-before-public-cue");
                s.Deadline(); s.Move(Phase.Trial);
                for (int i = 0; i < s.readers.Length; i++)
                {
                    var r = s.readers[i]; r.CheckIdentity(); Need((bool)r.canBegin.GetValue(r.body), "next-cue-authority");
                    r.cueFired = true; // One attempt stamped BEFORE this public write; no second trigger/retry.
                    r.body.React(CharacterPresentation.Reaction.Farewell);
                }
            }
            catch (System.Exception error) { s.Fail(Code("fire", error)); }
        }

        private void Bind(CharacterPresentation owner, Animator animator, int bodyIndex, Session s)
        {
            body = owner; live = animator; index = bodyIndex; session = s; boundFrame = Time.frameCount;
            var capture = typeof(CharacterPresentation).GetMethod("TryGetFarewellBody", Flags);
            holds = typeof(CharacterPresentation).GetMethod("HoldsFarewellBody", Flags);
            foreign = typeof(CharacterPresentation).GetMethod("ForeignFarewellCue", Flags);
            canBegin = typeof(CharacterPresentation).GetProperty("CanBeginFarewell", Flags);
            Need(capture != null && holds != null && foreign != null && canBegin != null, "existing-identity-cue-api");
            var args = new object[] { null }; Need((bool)capture.Invoke(body, args), "initial-identity"); identity = args[0];
            Need(identity != null && live.gameObject == gameObject && (Animator)Field("Animator") == live, "captured-actual-Animator-host");
            actor = body.CharacterId; animatorWitness = Id(live); avatarWitness = Id(live.avatar);
            revisionWitness = Field("RequestRevision").ToString(); frame = body.Frame; scene = body.gameObject.scene; culling = live.cullingMode;
            Need(live.layerCount == 1, "single-reviewed-native-layer");
            beforeReaction = body.LastReaction; beforeGesture = body.LastGesture;
            rootAnchor = body.transform.position; headingAnchor = body.transform.rotation;
            timeScale = Time.timeScale;
            Need(Finite(timeScale) && timeScale > 0f && Finite(rootAnchor) && Valid(headingAnchor), "initial-clock-root");
            Need(body.TryReadFarewell(out var pose) && pose.Idle && (bool)canBegin.GetValue(body), "actual-initial-settled-Idle");
            clips.Clear(); live.GetCurrentAnimatorClipInfo(0, clips);
            Need(clips.Count == 1 && clips[0].clip != null && Positive(clips[0].weight), "actual-idle-object"); idleClip = clips[0].clip;
            initialIdlePhase = live.GetCurrentAnimatorStateInfo(0).normalizedTime;
            Need(Finite(initialIdlePhase) && initialIdlePhase >= 0f, "initial-native-Idle-phase");
            var handle = Resources.Load<AnimatorOverrideController>("Animation/GamesimHumanoid");
            Need(handle != null && handle.runtimeAnimatorController != null, "base-controller-handle");
            var takes = handle.runtimeAnimatorController.animationClips.Where(c => c != null && c.name == CharacterPresentation.FarewellTake).ToArray();
            Need(takes.Length == 1 && takes[0].humanMotion && !takes[0].isLooping && Mathf.Abs(takes[0].length - 1f) <= .001f, "exact-one-second-take");
            takeClip = takes[0];
            for (int i = 0; i < bones.Length; i++) { bones[i] = live.GetBoneTransform(BoneNames[i]); Need(bones[i] != null, "actual-foot-toe-mapping"); }
            hosts = gameObject.GetComponents<MonoBehaviour>(); Need(hosts.Length <= HostCap, "host-cap");
            enabledHosts = new bool[hosts.Length]; activeHosts = new bool[hosts.Length];
            for (int i = 0; i < hosts.Length; i++)
            {
                Need(hosts[i] != null, "host-missing-script"); enabledHosts[i] = hosts[i].enabled; activeHosts[i] = hosts[i].isActiveAndEnabled;
                string type = hosts[i].GetType().FullName;
                Need(hosts[i] == this || type == "Gamesim.Presentation.CharacterBodyBuildState"
                    || type == "UMA.CharacterSystem.DynamicCharacterAvatar" || type == "Gamesim.Uma.UmaBodyTint"
                    || type == "Gamesim.Uma.UmaExpressions" || type == "UMA.PoseTools.UMAExpressionPlayer", "unreviewed-host-writer");
                session.Line("HOST body=" + index + " type=" + type + " instance=" + Id(hosts[i])
                    + " enabled=" + enabledHosts[i] + " active=" + activeHosts[i] + "\n");
            }
            session.Line("IDENTITY body=" + index + " actor=" + actor + " actualFrame=" + frame
                + " scene=" + scene.handle + " animator=" + Id(live) + " controller=" + Id(live.runtimeAnimatorController)
                + " avatar=" + Id(live.avatar) + " visual=" + Id((Object)Field("Visual"))
                + " provided=" + Id((Object)Field("ProvidedRoot")) + " build=" + Id((Object)Field("Build"))
                + " revision=" + Field("RequestRevision") + " idle=" + Id(idleClip) + " take=" + Id(takeClip)
                + " root=" + V(rootAnchor) + " heading=" + Q(headingAnchor) + " culling=" + culling
                + " boundFrame=" + boundFrame + " initialIdlePhase=" + N(initialIdlePhase)
                + " hostChannelLease=test-private otherHostBridgesUnproved=true\n");
        }

        private object Field(string name)
        { var f = identity.GetType().GetField(name, Flags); Need(f != null, "identity-field"); return f.GetValue(identity); }

        // Acceptance/presentation conditions stop target writes, but are not themselves a new foot-channel owner.
        private void CheckIdentity()
        {
            CheckChannelLease();
            Need(body.isActiveAndEnabled && !body.ReducedMotion && !body.IsPrimitiveBody && !body.IsBodyAssembling && !body.IsChangingOutfit
                && !body.IsSeated && !body.IsTalking && !body.IsArguing && body.Activity == CharacterPresentation.BodyActivity.None
                && body.IsStill && body.LastGesture == beforeGesture
                && body.LastReaction == (cueFired ? CharacterPresentation.Reaction.Farewell : beforeReaction)
                && !(bool)foreign.Invoke(body, new object[] { (int)CharacterPresentation.Reaction.Farewell }), "captured-presentation-cue");
            Need(Time.timeScale == timeScale && Finite(Time.deltaTime) && Time.deltaTime > 0f, "normal-positive-clock");
            Need(Finite(body.transform.position) && Valid(body.transform.rotation)
                && Vector3.Distance(rootAnchor, body.transform.position) <= RootBound
                && Angle(headingAnchor, body.transform.rotation) <= HeadingBound, "prewrite-root-heading");
            foreach (var host in hosts)
                if (host.GetType().FullName == "UMA.PoseTools.UMAExpressionPlayer")
                {
                    var gaze = host.GetType().GetField("gazeWeight", Flags);
                    Need(gaze != null && gaze.FieldType == typeof(float) && (float)gaze.GetValue(host) == 0f, "no-concurrent-UMA-gaze");
                }
        }

        // Narrow release authority: the same captured fixture body/Animator/channel, NEVER a spatial/clock repair.
        private void CheckChannelLease()
        {
            Need(body != null && live != null && body.CharacterId == actor
                && body.Frame == frame && body.gameObject.scene == scene && live.gameObject == gameObject
                && (bool)holds.Invoke(body, new[] { identity }) && live.cullingMode == culling, "body-revision-channel-identity");
            currentHosts.Clear(); live.GetComponents(currentHosts);
            Need(currentHosts.Count == hosts.Length && live.GetComponents<GroundedFarewellCalibratedIkProbe>().Length == 1, "exact-host-lease");
            for (int i = 0; i < hosts.Length; i++)
            {
                Need(hosts[i] != null && currentHosts[i] == hosts[i] && hosts[i].enabled == enabledHosts[i]
                    && hosts[i].isActiveAndEnabled == activeHosts[i], "host-membership-and-activation");
            }
            for (int i = 0; i < bones.Length; i++) Need(bones[i] != null && live.GetBoneTransform(BoneNames[i]) == bones[i], "same-four-bones");
        }

        private NativeSelection Selection()
        {
            Need(body.TryReadFarewell(out var pose), "actual-selected-pose");
            var current = live.GetCurrentAnimatorStateInfo(0); bool transition = live.IsInTransition(0);
            var next = transition ? live.GetNextAnimatorStateInfo(0) : default;
            Need(Finite(current.normalizedTime) && (!transition || Finite(next.normalizedTime)), "finite-selection-phase");
            float Clip(bool incoming, bool farewell)
            {
                clips.Clear(); if (incoming) live.GetNextAnimatorClipInfo(0, clips); else live.GetCurrentAnimatorClipInfo(0, clips);
                Need(clips.Count == 1 && clips[0].clip == (farewell ? takeClip : idleClip)
                    && Finite(clips[0].weight) && clips[0].weight >= 0f && clips[0].weight <= 1f, "exact-selected-clip-object-weight");
                return clips[0].weight;
            }
            Need(current.IsName("Idle") || current.IsName(CharacterPresentation.FarewellState), "no-foreign-current-state");
            float currentWeight = Clip(false, current.IsName(CharacterPresentation.FarewellState)), nextWeight = 0f;
            if (transition)
            {
                Need(current.IsName("Idle") && next.IsName(CharacterPresentation.FarewellState)
                    || current.IsName(CharacterPresentation.FarewellState) && next.IsName("Idle"), "exact-incoming-outgoing-crossing");
                nextWeight = Clip(true, next.IsName(CharacterPresentation.FarewellState));
            }
            else Need(pose.Idle || pose.InTake, "positive-settled-selection");
            return new NativeSelection(pose, current, next, transition, currentWeight, nextWeight);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (session == null || session.finalizing || !session.started) return;
            if (session.failure != null && !session.Releasing) return;
            if (!warmReady && !session.Releasing && Time.frameCount <= boundFrame) return;
            callbacks++; callbackStage = session.phase; callbackSample = session.sample;
            callbackMaxWarm = maxWarm; callbackMaxRoot = maxRoot; callbackMaxHeading = maxHeading;
            var release = ActiveRelease;
            if (session.Releasing && release.refusal != null) return; // One refused cleanup is never retried.
            preMask = targetMask = beforeBoneMask = finalBoneMask = postWeightMask = postGoalMask = setterStampMask = 0;
            selectionRead = callbackHandRead = lateHandRead = false;
            modelPredictionMask = modelResidualMask = 0;
            callbackRightHand = lateRightHand = default;
            try
            {
                Need(callbacks <= CallbackCap && layerIndex == 0 && !pending && callbackFrame != Time.frameCount,
                    "one-normal-callback-per-frame");
                callbackFrame = Time.frameCount;
                session.Deadline();
                preRoot = body != null ? body.transform.position : default;
                preHeading = body != null ? body.transform.rotation : default;
                if (Finite(preRoot)) maxRoot = Mathf.Max(maxRoot, Vector3.Distance(rootAnchor, preRoot));
                if (Valid(preHeading)) maxHeading = Mathf.Max(maxHeading, (float)Angle(headingAnchor, preHeading));
                if (session.Releasing) CheckReleaseLease(); else CheckIdentity();
                callbackRelease = session.Releasing; callbackPlaying = session.Playing; callbackAfterRelease = release.written;
                for (int i = 0; i < 2; i++)
                {
                    prePositionWeight[i] = live.GetIKPositionWeight(Goals[i]);
                    preRotationWeight[i] = live.GetIKRotationWeight(Goals[i]);
                    Need(Weight(prePositionWeight[i]) && Weight(preRotationWeight[i]), "finite-foot-channel-weights");
                }
                if (callbackRelease) ReleaseInsideCallback(); // Owned release precedes optional geometry diagnostics.
                for (int i = 0; i < 4; i++)
                {
                    pre[i] = bones[i].position; preRotation[i] = bones[i].rotation;
                    Need(Finite(pre[i]) && Valid(preRotation[i]), "finite-pre-bone"); beforeBoneMask |= 1 << i;
                }
                for (int i = 0; i < 2; i++)
                {
                    goal[i] = live.GetIKPosition(Goals[i]); goalRotation[i] = live.GetIKRotation(Goals[i]);
                    Need(Finite(goal[i]) && Valid(goalRotation[i]), "finite-raw-goal-bone"); preMask |= 1 << i;
                }
                callbackSelection = Selection(); callbackPhase = callbackSelection.pose.NormalizedTime; selectionRead = true;
                callbackRightHand = callbackSelection.pose.RightHand; callbackHandRead = true;
                Need(Finite(callbackRightHand), "finite-callback-right-hand");
                Need(Vector3.Distance(rootAnchor, preRoot) <= RootBound && Angle(headingAnchor, preHeading) <= HeadingBound,
                    "original-bind-root-heading-every-callback");
                if (warmReady && !baselineReady)
                {
                    for (int i = 0; i < 4; i++) maxWarm = Mathf.Max(maxWarm, Vector3.Distance(calibrationAnchors[i], pre[i]));
                    Need(maxWarm <= WarmLandmarkBound, "prewrite-calibration-50mm-safety-not-planting");
                }
                if (callbackStage == Phase.Calibration || callbackStage == Phase.Trial)
                {
                    Need(session.failure == null && warmReady && (callbackPlaying ? baselineReady && cueFired : calSamples == callbackSample),
                        "one-calibration-pair-no-extra-active-sample");
                    for (int i = 0; i < 2; i++)
                        if (callbackPlaying) ComputeTarget(i); else ComputeCalibrationTarget(i);
                    CheckIdentity(); session.Deadline(); // BOTH geometries/bounds precede ANY setter.
                    for (int i = 0; i < 2; i++)
                        Need((ownedMask & (1 << i)) != 0
                            ? BinaryWeight(prePositionWeight[i]) && BinaryWeight(preRotationWeight[i])
                            : prePositionWeight[i] == 0f && preRotationWeight[i] == 0f, "owned-foot-channel-readback");
                    for (int i = 0; i < 2; i++)
                    {
                        BeforeTargetSetter(i, 0); live.SetIKPosition(Goals[i], target[i]);
                        BeforeTargetSetter(i, 1); live.SetIKRotation(Goals[i], targetRotation[i]);
                        BeforeTargetSetter(i, 2); live.SetIKPositionWeight(Goals[i], 1f);
                        BeforeTargetSetter(i, 3); live.SetIKRotationWeight(Goals[i], 1f);
                        ReadPostWeights(i);
                        Need(postPositionWeight[i] == 1f && postRotationWeight[i] == 1f, "active-weight-readback");
                        postGoal[i] = live.GetIKPosition(Goals[i]); postGoalRotation[i] = live.GetIKRotation(Goals[i]);
                        Need(Finite(postGoal[i]) && Valid(postGoalRotation[i]), "finite-post-goal-readback"); postGoalMask |= 1 << i;
                        Need(Vector3.Distance(postGoal[i], target[i]) <= FootBound
                            && Angle(postGoalRotation[i], targetRotation[i]) <= HeadingBound, "submitted-goal-readback-agreement");
                    }
                }
                else if (!callbackRelease)
                    Need(prePositionWeight.All(w => w == 0f) && preRotationWeight.All(w => w == 0f), "readonly-zero-foot-channels");
                pending = true;
            }
            catch (System.Exception error)
            {
                if (callbackStage == Phase.CalibrationRelease || callbackStage == Phase.TrialRelease)
                    release.refusal = Code("release-callback", error);
                session.Fail(Code("callback", error)); TryFailureRow("callback");
            }
        }

        private void BeforeTargetSetter(int foot, int setter)
        {
            Need(session.failure == null && session.phase == callbackStage, "global-target-authority-still-held");
            CheckIdentity(); session.Deadline();
            ownedMask |= 1 << foot; setterStampMask |= 1 << (foot * 4 + setter); setterAttempts++;
            // Ownership and the partial attempt are stamped BEFORE every public engine call.
        }
        private void ComputeCalibrationTarget(int i)
        {
            Need(callbackSelection.pose.Idle && !callbackSelection.transition && !callbackSelection.pose.InTake
                && callbackSelection.currentPhase > lastIdlePhase, "calibration-natural-settled-Idle");
            targetRotation[i] = callbackSample == 0 ? referenceGoalRotation[i] : Normalize(
                Quaternion.AngleAxis((float)CalibrationAngle, headingAnchor * (callbackSample == 1 ? Vector3.right : Vector3.forward))
                * referenceGoalRotation[i]);
            target[i] = calibrationAnchors[i] - (callbackSample == 0 ? Vector3.zero : targetRotation[i] * frozenOffset[i]);
            Need(callbackSample >= 0 && callbackSample <= 2 && (callbackSample == 0 || coefficientsFrozen)
                && Finite(target[i]) && Valid(targetRotation[i]) && Vector3.Distance(target[i], calibrationAnchors[i]) <= GoalBound
                && Angle(targetRotation[i], referenceGoalRotation[i]) <= GoalAngleBound, "bounded-predetermined-calibration-goal");
            targetMask |= 1 << i;
            // Holdout model predictions are computed only from held post-set readbacks at Late.
        }

        private void ComputeTarget(int i)
        {
            var v = pre[i + 2] - pre[i]; var desired = anchors[i + 2] - anchors[i];
            Need(Finite(v) && Finite(desired) && v.sqrMagnitude > 1e-6f && desired.sqrMagnitude > 1e-6f, "nondegenerate-toe-vectors");
            var toeLocal = Quaternion.Inverse(preRotation[i]) * v;
            var atBase = baseRotation[i] * toeLocal;
            Need(Vector3.Dot(atBase.normalized, desired.normalized) > -.999f, "no-ambiguous-halfturn-toe-alignment");
            var desiredFootRotation = Quaternion.FromToRotation(atBase, desired) * baseRotation[i];
            Need(coefficientsFrozen, "immutable-train-coefficients");
            offset[i] = frozenOffset[i]; offsetRotation[i] = frozenRotation[i];
            targetRotation[i] = Normalize(desiredFootRotation * Quaternion.Inverse(frozenRotation[i]));
            target[i] = anchors[i] - targetRotation[i] * frozenOffset[i];
            predictedFoot[i] = target[i] + targetRotation[i] * frozenOffset[i];
            predictedFootRotation[i] = Normalize(targetRotation[i] * frozenRotation[i]);
            predictedToe[i] = anchors[i] + desiredFootRotation * toeLocal;
            float predictionGap = Vector3.Distance(predictedToe[i], anchors[i + 2]);
            Need(Finite(offset[i]) && Valid(offsetRotation[i]) && Valid(targetRotation[i]) && Finite(target[i])
                && Finite(predictedToe[i]) && Finite(predictionGap), "finite-dynamic-target"); targetMask |= 1 << i;
            maxPrediction = Mathf.Max(maxPrediction, predictionGap);
            Need(predictionGap <= FootBound, "rigid-toe-target-infeasible");
            bool actualDemand = Vector3.Distance(pre[i], anchors[i]) > FootBound || Vector3.Distance(pre[i + 2], anchors[i + 2]) > FootBound;
            demand |= actualDemand; positiveTakeDemand |= actualDemand && callbackSelection.pose.InTake
                && Positive(callbackSelection.pose.SelectedClipWeight);
        }

        private void CheckReleaseLease()
        {
            CheckChannelLease();
            Need(body.LastGesture == beforeGesture
                && body.LastReaction == (cueFired ? CharacterPresentation.Reaction.Farewell : beforeReaction)
                && !(bool)foreign.Invoke(body, new object[] { (int)CharacterPresentation.Reaction.Farewell }),
                "no-foreign-cue-channel-owner-during-release");
            foreach (var host in hosts)
                if (host.GetType().FullName == "UMA.PoseTools.UMAExpressionPlayer")
                {
                    var gaze = host.GetType().GetField("gazeWeight", Flags);
                    Need(gaze != null && gaze.FieldType == typeof(float) && (float)gaze.GetValue(host) == 0f, "no-foreign-gaze-release-owner");
                }
        }
        private void ReleaseInsideCallback()
        {
            var r = ActiveRelease;
            if (r.written)
            {
                Need(prePositionWeight.All(w => w == 0f) && preRotationWeight.All(w => w == 0f), "later-zero-foot-channels");
                return; // Never re-zero a later owner; calibration and trial have separate one-shot tokens.
            }
            Need(!r.attempted, "release-already-refused-no-retry"); r.attempted = true;
            for (int i = 0; i < 2; i++)
            {
                if ((ownedMask & (1 << i)) == 0)
                { Need(prePositionWeight[i] == 0f && preRotationWeight[i] == 0f, "unowned-channel-must-be-zero"); continue; }
                Need(BinaryWeight(prePositionWeight[i]) && BinaryWeight(preRotationWeight[i]), "release-channel-was-replaced");
                CheckReleaseLease(); session.Deadline(); setterAttempts++; live.SetIKPositionWeight(Goals[i], 0f);
                CheckReleaseLease(); session.Deadline(); setterAttempts++; live.SetIKRotationWeight(Goals[i], 0f);
                ReadPostWeights(i);
                Need(postPositionWeight[i] == 0f && postRotationWeight[i] == 0f, "release-zero-readback");
                ownedMask &= ~(1 << i);
            }
            r.written = true;
        }

        private void ReadPostWeights(int i)
        {
            postPositionWeight[i] = live.GetIKPositionWeight(Goals[i]);
            postRotationWeight[i] = live.GetIKRotationWeight(Goals[i]);
            Need(Weight(postPositionWeight[i]) && Weight(postRotationWeight[i]), "finite-post-weight-readback");
            postWeightMask |= 1 << i;
        }

        private void LateUpdate()
        {
            if (session == null || session.finalizing || !pending) return;
            pending = false;
            try
            {
                Need(callbackFrame == Time.frameCount && lateFrame != Time.frameCount, "actual-same-frame-LateUpdate");
                if (callbackRelease) CheckReleaseLease(); else CheckIdentity();
                Need(Time.timeScale == timeScale && Finite(Time.deltaTime) && Time.deltaTime > 0f,
                    "unchanged-positive-clock-every-late");
                var selected = Selection(); var pose = selected.pose; var beforePose = callbackSelection.pose;
                lateRightHand = pose.RightHand; lateHandRead = true;
                Need(Finite(lateRightHand), "finite-actual-late-right-hand");
                Need(pose.InTake == beforePose.InTake && pose.Idle == beforePose.Idle
                    && pose.Boundary == beforePose.Boundary && pose.NormalizedTime == callbackPhase
                    && pose.SelectedClipWeight == beforePose.SelectedClipWeight
                    && selected.currentHash == callbackSelection.currentHash && selected.nextHash == callbackSelection.nextHash
                    && selected.currentPhase == callbackSelection.currentPhase && selected.nextPhase == callbackSelection.nextPhase
                    && selected.currentWeight == callbackSelection.currentWeight && selected.nextWeight == callbackSelection.nextWeight
                    && selected.transition == callbackSelection.transition, "same-native-selection-evaluation");
                for (int i = 0; i < 4; i++)
                {
                    final[i] = bones[i].position; finalRotation[i] = bones[i].rotation;
                    Need(Finite(final[i]) && Valid(finalRotation[i]), "finite-final-bone"); finalBoneMask |= 1 << i;
                }
                finalRoot = pose.Root; finalHeading = pose.Heading;
                session.Deadline();
                lateFrame = Time.frameCount; lateReads++;
                maxRoot = Mathf.Max(maxRoot, Vector3.Distance(rootAnchor, finalRoot));
                maxHeading = Mathf.Max(maxHeading, (float)Angle(headingAnchor, finalHeading));
                Need(maxRoot <= RootBound && maxHeading <= HeadingBound, "original-bind-root-heading-every-late");
                if (warmReady && !baselineReady)
                {
                    for (int i = 0; i < 4; i++)
                        maxWarm = Mathf.Max(maxWarm, Vector3.Distance(calibrationAnchors[i], pre[i]),
                            Vector3.Distance(calibrationAnchors[i], final[i]));
                    Need(maxWarm <= WarmLandmarkBound, "calibration-release-settle-50mm-safety-not-planting");
                }
                if (maxWarm > callbackMaxWarm || maxRoot > callbackMaxRoot || maxHeading > callbackMaxHeading)
                    Record("NEW-WARM-OR-ROOT-EXTREMUM", true);
                if (callbackStage == Phase.Reference && !warmReady)
                {
                    Need(pose.Idle && !pose.InTake && !selected.transition
                        && pose.Boundary == CharacterPresentation.FarewellBoundary.None, "reference-positive-Idle");
                    Need(Time.frameCount > boundFrame && selected.currentPhase > initialIdlePhase, "naturally-advanced-reference-Idle");
                    for (int i = 0; i < 4; i++)
                    { calibrationAnchors[i] = final[i]; }
                    for (int i = 0; i < 2; i++) referenceGoalRotation[i] = Normalize(goalRotation[i]);
                    referenceFrame = Time.frameCount; lastIdlePhase = selected.currentPhase; warmReady = true;
                    Record("CALIBRATION-REFERENCE-C", true);
                }
                else if (callbackStage == Phase.Calibration)
                {
                    Need(session.failure == null && callbackSample == session.sample && calSamples == callbackSample,
                        "exact-shared-calibration-sample");
                    if (callbackSample == 0)
                    {
                        Need(!coefficientsFrozen, "train-only-once");
                        for (int i = 0; i < 2; i++)
                        {
                            frozenOffset[i] = Quaternion.Inverse(Normalize(postGoalRotation[i])) * (final[i] - postGoal[i]);
                            frozenRotation[i] = Normalize(Quaternion.Inverse(Normalize(postGoalRotation[i])) * Normalize(finalRotation[i]));
                            Need(Finite(frozenOffset[i]) && frozenOffset[i].magnitude <= OffsetBound
                                && Valid(frozenRotation[i]), "bounded-train-coefficient");
                            predictedFoot[i] = postGoal[i] + postGoalRotation[i] * frozenOffset[i];
                            predictedFootRotation[i] = Normalize(postGoalRotation[i] * frozenRotation[i]);
                        }
                        coefficientsFrozen = true; // No assignment to these arrays at holdouts, cue, failure or cleanup.
                    }
                    for (int i = 0; i < 2; i++)
                    {
                        if (callbackSample > 0)
                        {
                            // Frozen model evaluated on actual same-evaluation post-set G, not submitted targets.
                            predictedFoot[i] = postGoal[i] + postGoalRotation[i] * frozenOffset[i];
                            predictedFootRotation[i] = Normalize(postGoalRotation[i] * frozenRotation[i]);
                        }
                        modelPredictionMask |= 1 << i;
                        positionResidual[i] = Vector3.Distance(predictedFoot[i], final[i]);
                        angleResidual[i] = Angle(predictedFootRotation[i], finalRotation[i]);
                        Need(Finite(positionResidual[i]) && Finite(angleResidual[i]), "finite-heldout-residual");
                        modelResidualMask |= 1 << i;
                    }
                    Record(callbackSample == 0 ? "TRAIN-0-TAUTOLOGICAL" : "HELDOUT-" + callbackSample + "-LOCAL-CONSISTENCY", true);
                    if (callbackSample > 0)
                        for (int i = 0; i < 2; i++)
                            Need(positionResidual[i] <= FootBound && angleResidual[i] <= HeadingBound,
                                "heldout-local-position-orientation-inconsistent");
                    lastIdlePhase = selected.currentPhase; calSamples++; session.CalibrationBarrier();
                }
                else if (callbackStage == Phase.Settle && !baselineReady)
                {
                    Need(pose.Idle && !pose.InTake && !selected.transition
                        && pose.Boundary == CharacterPresentation.FarewellBoundary.None
                        && Time.frameCount > calibrationRelease.frame && selected.currentPhase > calibrationRelease.nativePhase
                        && prePositionWeight.All(w => w == 0f) && preRotationWeight.All(w => w == 0f),
                        "first-qualifying-post-release-settled-Idle");
                    for (int i = 0; i < 4; i++)
                    {
                        anchors[i] = final[i]; baseRotation[i] = finalRotation[i];
                        maxCtoA = Mathf.Max(maxCtoA, Vector3.Distance(calibrationAnchors[i], anchors[i]));
                    }
                    bodyBaselineHand = lateRightHand.y; baselineFrame = Time.frameCount; baselineReady = true;
                    Record("SOLE-FIRST-QUALIFYING-TRIAL-BASELINE-A", true); // Not selected for spatial closeness to C.
                }
                else if (callbackPlaying)
                {
                    float previousMax = maxFeet;
                    for (int i = 0; i < 4; i++) maxFeet = Mathf.Max(maxFeet, Vector3.Distance(anchors[i], final[i]));
                    Record("STRICT-SOLVED-INCLUDING-INCOMING-ZERO-WEIGHT", maxFeet > previousMax);
                    Need(maxFeet <= FootBound, "actual-final-foot-toe-1mm"); // Never exclude the first zero-weight boundary.
                    if (pose.InTake)
                    {
                        Need(Positive(pose.SelectedClipWeight) && pose.NormalizedTime >= lastTakePhase, "positive-natural-take-phase");
                        lastTakePhase = pose.NormalizedTime;
                        if (enteredFrame < 0) { enteredFrame = Time.frameCount; Record("FIRST-POSITIVE-TAKE", true); }
                        if (!neutral && pose.NormalizedTime >= CharacterPresentation.FarewellNeutral)
                        { neutral = true; Record("NATIVE-NEUTRAL-BOUNDARY", true); }
                        if (pose.NormalizedTime >= 10f / 30f && pose.NormalizedTime <= 17f / 30f
                            && lateRightHand.y > bodyBaselineHand + .03f) { palmSamples++; if (palmSamples == 1) Record("PALM-10-17", true); }
                    }
                    if (pose.Idle && enteredFrame >= 0 && !completed)
                    { Need(neutral && palmSamples > 0, "actual-neutral-and-palm-before-returned-Idle"); completed = true; Record("RETURNED-IDLE", true); }
                }
                else if (callbackRelease && ActiveRelease.written)
                {
                    var r = ActiveRelease;
                    Record(callbackAfterRelease ? "AFTER-OWNED-RELEASE" : "OWNED-RELEASE", true);
                    if (callbackAfterRelease)
                    {
                        Need(Time.frameCount > r.frame && (selected.currentHash != r.hash || selected.currentPhase > r.nativePhase),
                            "two-distinct-later-natural-zero-evaluations");
                        if (r.laterZeroFrames < 2) r.laterZeroFrames++;
                    }
                    r.frame = Time.frameCount; r.nativePhase = selected.currentPhase; r.hash = selected.currentHash;
                }
            }
            catch (System.Exception error)
            {
                if (callbackRelease) ActiveRelease.refusal = Code("release-late", error);
                session.Fail(Code("late", error)); TryFailureRow("late");
            }
        }

        private float bodyBaselineHand = float.NegativeInfinity;
        private void Record(string stage, bool force)
        {
            float now = Time.realtimeSinceStartup;
            if (!force && now - lastRow < 1f / 60f) return;
            lastRow = now; session.Line(Row(stage), true);
        }
        private string Row(string stage)
        {
            var row = new System.Text.StringBuilder(2048);
            row.Append("ROW body=").Append(index).Append(" actor=").Append(actor).Append(" animator=").Append(animatorWitness)
                .Append(" avatar=").Append(avatarWitness).Append(" revision=").Append(revisionWitness).Append(" stage=").Append(stage)
                .Append(" phase=").Append(callbackStage).Append(" sample=").Append(callbackSample)
                .Append(" callbackFrame=").Append(callbackFrame).Append(" lateFrame=").Append(lateFrame)
                .Append(" owned=").Append(ownedMask).Append(" setterStamp=").Append(setterStampMask)
                .Append(" masks=").Append(preMask).Append('/').Append(targetMask).Append('/').Append(beforeBoneMask)
                .Append('/').Append(finalBoneMask).Append('/').Append(postWeightMask).Append('/').Append(postGoalMask)
                .Append(" selectionRead=").Append(selectionRead).Append(" boundary=").Append(callbackSelection.pose.Boundary)
                .Append(" nativePhase=").Append(N(callbackPhase)).Append(" weight=").Append(N(callbackSelection.pose.SelectedClipWeight))
                .Append(" referenceFrame=").Append(referenceFrame).Append(" AFrame=").Append(baselineFrame)
                .Append(" callbackHandRead=").Append(callbackHandRead).Append(" callbackHand=").Append(callbackHandRead ? V(callbackRightHand) : "unread")
                .Append(" lateHandRead=").Append(lateHandRead).Append(" lateHand=").Append(lateHandRead ? V(lateRightHand) : "unread")
                .Append(" handBaseline=").Append(N(bodyBaselineHand))
                .Append(" preRoot=").Append(V(preRoot)).Append(" preHeading=").Append(Q(preHeading))
                .Append(" root=").Append(V(finalRoot)).Append(" heading=").Append(Q(finalHeading))
                .Append(" warmMax=").Append(N(maxWarm)).Append(" CtoA=").Append(N(maxCtoA)).Append(" trialMax=").Append(N(maxFeet))
                .Append(" calRelease=").Append(calibrationRelease.attempted).Append('/').Append(calibrationRelease.written)
                .Append('/').Append(calibrationRelease.laterZeroFrames).Append('/').Append(calibrationRelease.frame)
                .Append(" trialRelease=").Append(trialRelease.attempted).Append('/').Append(trialRelease.written)
                .Append('/').Append(trialRelease.laterZeroFrames).Append('/').Append(trialRelease.frame);
            for (int i = 0; i < 2; i++)
                row.Append(" foot").Append(i).Append(" preWeights=").Append(N(prePositionWeight[i])).Append('/').Append(N(preRotationWeight[i]))
                    .Append(" postWeights=").Append(N(postPositionWeight[i])).Append('/').Append(N(postRotationWeight[i]))
                    .Append(" rawGoal=").Append(V(goal[i])).Append('/').Append(Q(goalRotation[i]))
                    .Append(" target=").Append(V(target[i])).Append('/').Append(Q(targetRotation[i]))
                    .Append(" readback=").Append(V(postGoal[i])).Append('/').Append(Q(postGoalRotation[i]))
                    .Append(" frozen=").Append(V(frozenOffset[i])).Append('/').Append(Q(frozenRotation[i]))
                    .Append(" modelPredictionMask=").Append(modelPredictionMask)
                    .Append(" postGoalModelPrediction=").Append((modelPredictionMask & (1 << i)) != 0 ? V(predictedFoot[i]) + "/" + Q(predictedFootRotation[i]) : "unread")
                    .Append(" trialCommandPrediction=").Append(callbackStage == Phase.Trial && (targetMask & (1 << i)) != 0 ? V(predictedFoot[i]) + "/" + Q(predictedFootRotation[i]) : "unread")
                    .Append(" modelResidualMask=").Append(modelResidualMask)
                    .Append(" modelResidual=").Append((modelResidualMask & (1 << i)) != 0 ? D(positionResidual[i]) + "/" + D(angleResidual[i]) : "unread")
                    .Append(" toeLocal=").Append(V(Quaternion.Inverse(preRotation[i]) * (pre[i + 2] - pre[i])));
            for (int i = 0; i < 4; i++)
                row.Append(" landmark").Append(i).Append(" C=").Append(V(calibrationAnchors[i]))
                    .Append(" A=").Append(V(anchors[i])).Append(" pre=").Append(V(pre[i])).Append(" final=").Append(V(final[i]))
                    .Append(" Q=").Append(Q(finalRotation[i]));
            row.Append('\n'); return row.ToString();
        }
        private void TryFailureRow(string stage)
        {
            try
            {
                if (session.firstFailureRow == null)
                {
                    string row = Row("FIRST-FAILURE-" + stage);
                    Need(System.Text.Encoding.UTF8.GetByteCount(row) <= 3072, "reserved-first-failure-row-cap");
                    session.firstFailureRow = row;
                }
            }
            catch (System.Exception error) { session.diagnosticFailure = Code("failure-row", error); session.Fail(session.diagnosticFailure); }
        }
        private static string Code(string stage, System.Exception error) => stage + ":"
            + (error is Refusal refusal ? refusal.code : error.GetType().Name);
        private sealed class Refusal : System.Exception
        { internal readonly string code; internal Refusal(string value) : base("calibrated-ik-A:" + value) { code = value; } }
        private static void Need(bool value, string code) { if (!value) throw new Refusal(code); }
        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        private static string D(double v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        private static double Norm(Quaternion q)
        {
            Need(Valid(q), "finite-unit-quaternion-input");
            double x = q.x, y = q.y, z = q.z, w = q.w;
            double n = System.Math.Sqrt(x * x + y * y + z * z + w * w);
            Need(Finite(n) && n > 0d, "nonzero-finite-quaternion-norm"); return n;
        }
        private static Quaternion Normalize(Quaternion q)
        {
            double n = Norm(q);
            var result = new Quaternion((float)(q.x / n), (float)(q.y / n), (float)(q.z / n), (float)(q.w / n));
            Need(Valid(result), "finite-normalized-quaternion"); return result;
        }
        private static double Angle(Quaternion a, Quaternion b)
        {
            double na = Norm(a), nb = Norm(b);
            double dot = ((double)a.x * b.x + (double)a.y * b.y + (double)a.z * b.z + (double)a.w * b.w) / (na * nb);
            Need(Finite(dot), "finite-double-normalized-dot");
            dot = System.Math.Max(0d, System.Math.Min(1d, System.Math.Abs(dot)));
            double degrees = 2d * System.Math.Acos(dot) * (180d / System.Math.PI);
            Need(Finite(degrees), "finite-double-angular-residual"); return degrees;
        }
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        private static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        private static bool Valid(Quaternion q) => Finite(q.x) && Finite(q.y) && Finite(q.z) && Finite(q.w)
            && Mathf.Abs(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w - 1f) <= .001f;
        private static bool Weight(float v) => Finite(v) && v >= 0f && v <= 1f;
        private static bool BinaryWeight(float v) => v == 0f || v == 1f;
        private static bool Positive(float v) => Weight(v) && v > 0f;
        private static string N(float v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        private static string V(Vector3 v) => N(v.x) + "," + N(v.y) + "," + N(v.z);
        private static string Q(Quaternion q) => N(q.x) + "," + N(q.y) + "," + N(q.z) + "," + N(q.w);
        private static string Id(Object value) => value == null ? "null" : value.GetEntityId().ToString();
    }

    /// <summary>TEST ONLY. Per-evaluation mapping is an unproved hypothesis; no production admission.</summary>
    [DefaultExecutionOrder(200)]
    internal sealed class GroundedFarewellDynamicIkFeasibilityProbe : MonoBehaviour
    {
        private const float WarmSeconds = .5f, TakeSeconds = 2f, ReleaseSeconds = .5f;
        private const float FootBound = .001f, RootBound = .001f, HeadingBound = .05f;
        private const int CallbackCap = 32768, HostCap = 16;
        private static readonly HumanBodyBones[] BoneNames =
            { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes };
        private static readonly AvatarIKGoal[] Goals = { AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot };
        private const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

        private sealed class Session
        {
            internal readonly GroundedFarewellDynamicIkFeasibilityProbe[] readers = new GroundedFarewellDynamicIkFeasibilityProbe[2];
            internal readonly System.Text.StringBuilder text = new System.Text.StringBuilder(4096);
            internal bool playing, releasing, success, finalizing;
            internal float warmAt, takeAt, releaseAt;
            internal string failure, diagnosticFailure;
            private int bytes, rows;
            private const int ByteCap = 4 * 1024 * 1024, RowCap = 1024, Reserve = 4096;
            internal void Line(string line, bool row = false)
            {
                int size = System.Text.Encoding.UTF8.GetByteCount(line);
                Need((!row || rows < RowCap) && size <= ByteCap - Reserve - bytes, "report-cap");
                text.Append(line); bytes += size; if (row) rows++;
            }
            internal void Release()
            {
                if (releasing) return;
                playing = false; releasing = true; releaseAt = Time.realtimeSinceStartup;
            }
            internal void Fail(string stage)
            {
                if (failure == null) failure = stage;
                Release(); // Target writes stop globally BEFORE any subsequent endpoint callback.
            }
            internal void Finish()
            {
                var tail = new System.Text.StringBuilder(2048);
                tail.Append("RESULT siteFeasibilityPassed=").Append(success ? "true" : "false")
                    .Append(" failure=").Append(failure ?? "none")
                    .Append(" diagnosticFailure=").Append(diagnosticFailure ?? "none")
                    .Append(" production=false gameRoute=false fullSuite=false visual=false\n");
                for (int i = 0; i < readers.Length; i++)
                {
                    var r = readers[i];
                    if (r == null) { tail.Append("BODY ").Append(i).Append(" UNREACHED\n"); continue; }
                    tail.Append("BODY ").Append(i).Append(" callbacks=").Append(r.callbacks)
                        .Append(" completedLate=").Append(r.lateReads).Append(" settersAttempted=").Append(r.setterAttempts)
                        .Append(" anchorsCaptured=").Append(r.warmReady).Append(" enteredFrame=").Append(r.enteredFrame)
                        .Append(" neutral=").Append(r.neutral).Append(" palmSamples=").Append(r.palmSamples)
                        .Append(" returnedIdle=").Append(r.completed).Append(" genericSensedDemand=").Append(r.demand)
                        .Append(" positiveTakeDemand=").Append(r.positiveTakeDemand)
                        .Append(" releaseAttempted=").Append(r.releaseAttempted).Append(" releaseWritten=").Append(r.releaseWritten)
                        .Append(" laterZeroFrames=").Append(r.laterZeroFrames).Append(" remainingOwnedMask=").Append(r.ownedMask)
                        .Append(" releaseRefusal=").Append(r.releaseRefusal ?? "none")
                        .Append(" maxFeet=").Append(N(r.maxFeet)).Append(" maxRoot=").Append(N(r.maxRoot))
                        .Append(" maxHeading=").Append(N(r.maxHeading)).Append(" toePrediction=").Append(N(r.maxPrediction))
                        .Append(" mappingQualified=false allWriterOrderQualified=false\n");
                }
                string value = tail.ToString(); int size = System.Text.Encoding.UTF8.GetByteCount(value);
                Need(size <= Reserve && bytes + size <= ByteCap, "report-tail-cap"); text.Append(value);
            }
        }

        private CharacterPresentation body;
        private Animator live;
        private object identity;
        private System.Reflection.MethodInfo holds, foreign;
        private System.Reflection.PropertyInfo canBegin;
        private BodyFrame frame;
        private AnimatorCullingMode culling;
        private CharacterPresentation.Reaction? beforeReaction;
        private CharacterPresentation.Gesture? beforeGesture;
        private UnityEngine.SceneManagement.Scene scene;
        private string actor;
        private AnimationClip idleClip, takeClip;
        private Session session;
        private int index, callbacks, lateReads, setterAttempts, callbackFrame = -1, lateFrame = -1, boundFrame;
        private int enteredFrame = -1, palmSamples, ownedMask, preMask, targetMask, laterZeroFrames;
        private int beforeBoneMask, finalBoneMask, postWeightMask, postGoalMask;
        private bool selectionRead;
        private bool pending, warmReady, cueFired, neutral, completed, demand, positiveTakeDemand, releaseAttempted, releaseWritten;
        private bool callbackRelease, callbackAfterRelease, callbackPlaying;
        private string releaseRefusal;
        private float timeScale, lastRow = float.NegativeInfinity, lastTakePhase = -1f, releasePhase, callbackPhase, initialIdlePhase;
        private float maxFeet, maxRoot, maxHeading, maxPrediction;
        private Vector3 rootAnchor;
        private Quaternion headingAnchor;
        private Vector3 preRoot, finalRoot;
        private Quaternion preHeading, finalHeading;
        private readonly struct NativeSelection
        {
            internal readonly CharacterPresentation.FarewellObservation pose;
            internal readonly int currentHash, nextHash;
            internal readonly float currentPhase, nextPhase, currentWeight, nextWeight;
            internal readonly bool transition;
            internal NativeSelection(CharacterPresentation.FarewellObservation value, AnimatorStateInfo current,
                AnimatorStateInfo next, bool crossing, float weight, float incomingWeight)
            { pose = value; currentHash = current.fullPathHash; nextHash = next.fullPathHash;
                currentPhase = current.normalizedTime; nextPhase = next.normalizedTime; transition = crossing;
                currentWeight = weight; nextWeight = incomingWeight; }
        }
        private NativeSelection callbackSelection;
        private int releaseHash;
        private readonly Transform[] bones = new Transform[4];
        private readonly Vector3[] anchors = new Vector3[4], pre = new Vector3[4], final = new Vector3[4];
        private readonly Quaternion[] baseRotation = new Quaternion[4], preRotation = new Quaternion[4], finalRotation = new Quaternion[4];
        private readonly Vector3[] goal = new Vector3[2], target = new Vector3[2], offset = new Vector3[2], predictedToe = new Vector3[2];
        private readonly Quaternion[] goalRotation = new Quaternion[2], targetRotation = new Quaternion[2], offsetRotation = new Quaternion[2];
        private readonly Vector3[] postGoal = new Vector3[2];
        private readonly Quaternion[] postGoalRotation = new Quaternion[2];
        private readonly float[] prePositionWeight = new float[2], preRotationWeight = new float[2];
        private readonly float[] postPositionWeight = new float[2], postRotationWeight = new float[2];
        private MonoBehaviour[] hosts;
        private bool[] enabledHosts, activeHosts;
        private readonly System.Collections.Generic.List<MonoBehaviour> currentHosts = new System.Collections.Generic.List<MonoBehaviour>(HostCap);
        private readonly System.Collections.Generic.List<AnimatorClipInfo> clips = new System.Collections.Generic.List<AnimatorClipInfo>(2);

        public static IEnumerator Observe(CharacterPresentation[] bodies, string provenance, bool requireUmaFrames)
        {
            var s = new Session();
            s.Line("DYNAMIC-IK-A version=1 provenance=" + provenance
                + " originalBounds=.001m/.05deg anchorsReset=false animatorForced=false boneWrites=false"
                + " nativeMappingHypothesis=unproved lateOrder=200 wholeFrameFinal=false production=false\n");
            try
            {
                BindPair(s, bodies, requireUmaFrames);
                s.warmAt = Time.realtimeSinceStartup;
                while (s.failure == null && !s.readers.All(r => r.warmReady)
                    && Time.realtimeSinceStartup - s.warmAt <= WarmSeconds) yield return null;
                if (s.failure == null && !s.readers.All(r => r.warmReady)) s.Fail("missing-natural-warm-callback");
                if (s.failure == null) Fire(s);
                while (s.failure == null && !s.readers.All(r => r.completed)
                    && Time.realtimeSinceStartup - s.takeAt <= TakeSeconds) yield return null;
                if (s.failure == null && !s.readers.All(r => r.completed)) s.Fail("natural-take-deadline");
                if (s.failure == null)
                {
                    if (!s.readers.All(r => r.demand)) s.Fail("missing-genuine-greater-than-1mm-demand");
                    else if (s.readers[0].enteredFrame != s.readers[1].enteredFrame) s.Fail("different-actual-entry-frames");
                    else s.Release();
                }
                while (!s.readers.All(r => r == null || r.releaseWritten && r.laterZeroFrames >= 2)
                    && Time.realtimeSinceStartup - s.releaseAt <= ReleaseSeconds) yield return null;
                if (!s.readers.All(r => r == null || r.releaseWritten && r.laterZeroFrames >= 2)) s.Fail("release-or-later-zero-unqualified");
                s.success = s.failure == null && s.readers.All(r => r.completed && r.demand && r.neutral
                    && r.palmSamples > 0 && r.releaseWritten && r.laterZeroFrames >= 2 && r.ownedMask == 0);
            }
            finally
            {
                // No off-callback setter, broad cue cancellation, bone restoration or forced evaluation.
                // Incomplete release stays FAILED; fixture-owned bodies are later destroyed by existing TearDown.
                s.finalizing = true;
                foreach (var r in s.readers) if (r != null) r.enabled = false;
                try { s.Finish(); Debug.Log(s.text.ToString()); }
                finally { foreach (var r in s.readers) if (r != null) Object.Destroy(r); }
            }
            Assert.That(s.success, Is.True, "Failure-capable active mapping experiment: " + (s.failure ?? "completion evidence missing"));
        }

        private static void BindPair(Session s, CharacterPresentation[] bodies, bool requireUmaFrames)
        {
            try
            {
                Need(bodies != null && bodies.Length == 2 && bodies[0] != bodies[1], "two-distinct-bodies");
                if (requireUmaFrames)
                    Need(bodies[0] != null && bodies[1] != null && bodies[0].Frame == BodyFrame.Masculine
                        && bodies[1].Frame == BodyFrame.Feminine, "actual-masculine-feminine-UMA-frames");
                for (int i = 0; i < 2; i++)
                {
                    var owner = bodies[i];
                    Need(owner != null && !owner.IsPrimitiveBody && owner.VisualRoot != null, "actual-provided-body");
                    var animator = owner.VisualRoot.GetComponentInChildren<Animator>(true);
                    Need(animator != null && animator.GetComponents<GroundedFarewellDynamicIkFeasibilityProbe>().Length == 0, "fresh-actual-host");
                    s.readers[i] = animator.gameObject.AddComponent<GroundedFarewellDynamicIkFeasibilityProbe>();
                    s.readers[i].Bind(owner, animator, i, s);
                }
            }
            catch (System.Exception error) { s.Fail(Code("bind", error)); }
        }

        private static void Fire(Session s)
        {
            try
            {
                foreach (var r in s.readers) { r.CheckIdentity(); Need((bool)r.canBegin.GetValue(r.body), "public-cue-preflight"); }
                s.takeAt = Time.realtimeSinceStartup; s.playing = true;
                for (int i = 0; i < s.readers.Length; i++)
                {
                    var r = s.readers[i]; r.CheckIdentity(); Need((bool)r.canBegin.GetValue(r.body), "next-cue-authority");
                    r.cueFired = true; // One attempt stamped BEFORE this public write; no second trigger/retry.
                    r.body.React(CharacterPresentation.Reaction.Farewell);
                }
            }
            catch (System.Exception error) { s.Fail(Code("fire", error)); }
        }

        private void Bind(CharacterPresentation owner, Animator animator, int bodyIndex, Session s)
        {
            body = owner; live = animator; index = bodyIndex; session = s; boundFrame = Time.frameCount;
            var capture = typeof(CharacterPresentation).GetMethod("TryGetFarewellBody", Flags);
            holds = typeof(CharacterPresentation).GetMethod("HoldsFarewellBody", Flags);
            foreign = typeof(CharacterPresentation).GetMethod("ForeignFarewellCue", Flags);
            canBegin = typeof(CharacterPresentation).GetProperty("CanBeginFarewell", Flags);
            Need(capture != null && holds != null && foreign != null && canBegin != null, "existing-identity-cue-api");
            var args = new object[] { null }; Need((bool)capture.Invoke(body, args), "initial-identity"); identity = args[0];
            Need(identity != null && live.gameObject == gameObject && (Animator)Field("Animator") == live, "captured-actual-Animator-host");
            actor = body.CharacterId; frame = body.Frame; scene = body.gameObject.scene; culling = live.cullingMode;
            Need(live.layerCount == 1, "single-reviewed-native-layer");
            beforeReaction = body.LastReaction; beforeGesture = body.LastGesture;
            rootAnchor = body.transform.position; headingAnchor = body.transform.rotation;
            timeScale = Time.timeScale;
            Need(Finite(timeScale) && timeScale > 0f && Finite(rootAnchor) && Valid(headingAnchor), "initial-clock-root");
            Need(body.TryReadFarewell(out var pose) && pose.Idle && (bool)canBegin.GetValue(body), "actual-initial-settled-Idle");
            clips.Clear(); live.GetCurrentAnimatorClipInfo(0, clips);
            Need(clips.Count == 1 && clips[0].clip != null && Positive(clips[0].weight), "actual-idle-object"); idleClip = clips[0].clip;
            initialIdlePhase = live.GetCurrentAnimatorStateInfo(0).normalizedTime;
            Need(Finite(initialIdlePhase) && initialIdlePhase >= 0f, "initial-native-Idle-phase");
            var handle = Resources.Load<AnimatorOverrideController>("Animation/GamesimHumanoid");
            Need(handle != null && handle.runtimeAnimatorController != null, "base-controller-handle");
            var takes = handle.runtimeAnimatorController.animationClips.Where(c => c != null && c.name == CharacterPresentation.FarewellTake).ToArray();
            Need(takes.Length == 1 && takes[0].humanMotion && !takes[0].isLooping && Mathf.Abs(takes[0].length - 1f) <= .001f, "exact-one-second-take");
            takeClip = takes[0];
            for (int i = 0; i < bones.Length; i++) { bones[i] = live.GetBoneTransform(BoneNames[i]); Need(bones[i] != null, "actual-foot-toe-mapping"); }
            hosts = gameObject.GetComponents<MonoBehaviour>(); Need(hosts.Length <= HostCap, "host-cap");
            enabledHosts = new bool[hosts.Length]; activeHosts = new bool[hosts.Length];
            for (int i = 0; i < hosts.Length; i++)
            {
                Need(hosts[i] != null, "host-missing-script"); enabledHosts[i] = hosts[i].enabled; activeHosts[i] = hosts[i].isActiveAndEnabled;
                string type = hosts[i].GetType().FullName;
                Need(hosts[i] == this || type == "Gamesim.Presentation.CharacterBodyBuildState"
                    || type == "UMA.CharacterSystem.DynamicCharacterAvatar" || type == "Gamesim.Uma.UmaBodyTint"
                    || type == "Gamesim.Uma.UmaExpressions" || type == "UMA.PoseTools.UMAExpressionPlayer", "unreviewed-host-writer");
                session.Line("HOST body=" + index + " type=" + type + " instance=" + Id(hosts[i])
                    + " enabled=" + enabledHosts[i] + " active=" + activeHosts[i] + "\n");
            }
            session.Line("IDENTITY body=" + index + " actor=" + actor + " actualFrame=" + frame
                + " scene=" + scene.handle + " animator=" + Id(live) + " controller=" + Id(live.runtimeAnimatorController)
                + " avatar=" + Id(live.avatar) + " visual=" + Id((Object)Field("Visual"))
                + " provided=" + Id((Object)Field("ProvidedRoot")) + " build=" + Id((Object)Field("Build"))
                + " revision=" + Field("RequestRevision") + " idle=" + Id(idleClip) + " take=" + Id(takeClip)
                + " root=" + V(rootAnchor) + " heading=" + Q(headingAnchor) + " culling=" + culling
                + " boundFrame=" + boundFrame + " initialIdlePhase=" + N(initialIdlePhase)
                + " hostChannelLease=test-private otherHostBridgesUnproved=true\n");
        }

        private object Field(string name)
        { var f = identity.GetType().GetField(name, Flags); Need(f != null, "identity-field"); return f.GetValue(identity); }

        // Acceptance/presentation conditions stop target writes, but are not themselves a new foot-channel owner.
        private void CheckIdentity()
        {
            CheckChannelLease();
            Need(body.isActiveAndEnabled && !body.ReducedMotion && !body.IsPrimitiveBody && !body.IsBodyAssembling && !body.IsChangingOutfit
                && !body.IsSeated && !body.IsTalking && !body.IsArguing && body.Activity == CharacterPresentation.BodyActivity.None
                && body.IsStill && body.LastGesture == beforeGesture
                && body.LastReaction == (cueFired ? CharacterPresentation.Reaction.Farewell : beforeReaction)
                && !(bool)foreign.Invoke(body, new object[] { (int)CharacterPresentation.Reaction.Farewell }), "captured-presentation-cue");
            Need(Time.timeScale == timeScale && Finite(Time.deltaTime) && Time.deltaTime > 0f, "normal-positive-clock");
            Need(Finite(body.transform.position) && Valid(body.transform.rotation)
                && Vector3.Distance(rootAnchor, body.transform.position) <= RootBound
                && Quaternion.Angle(headingAnchor, body.transform.rotation) <= HeadingBound, "prewrite-root-heading");
            foreach (var host in hosts)
                if (host.GetType().FullName == "UMA.PoseTools.UMAExpressionPlayer")
                {
                    var gaze = host.GetType().GetField("gazeWeight", Flags);
                    Need(gaze != null && gaze.FieldType == typeof(float) && (float)gaze.GetValue(host) == 0f, "no-concurrent-UMA-gaze");
                }
        }

        // Narrow release authority: the same captured fixture body/Animator/channel, NEVER a spatial/clock repair.
        private void CheckChannelLease()
        {
            Need(body != null && live != null && body.CharacterId == actor
                && body.Frame == frame && body.gameObject.scene == scene && live.gameObject == gameObject
                && (bool)holds.Invoke(body, new[] { identity }) && live.cullingMode == culling, "body-revision-channel-identity");
            currentHosts.Clear(); live.GetComponents(currentHosts);
            Need(currentHosts.Count == hosts.Length && live.GetComponents<GroundedFarewellDynamicIkFeasibilityProbe>().Length == 1, "exact-host-lease");
            for (int i = 0; i < hosts.Length; i++)
            {
                Need(hosts[i] != null && currentHosts[i] == hosts[i] && hosts[i].enabled == enabledHosts[i]
                    && hosts[i].isActiveAndEnabled == activeHosts[i], "host-membership-and-activation");
            }
            for (int i = 0; i < bones.Length; i++) Need(bones[i] != null && live.GetBoneTransform(BoneNames[i]) == bones[i], "same-four-bones");
        }

        private NativeSelection Selection()
        {
            Need(body.TryReadFarewell(out var pose), "actual-selected-pose");
            var current = live.GetCurrentAnimatorStateInfo(0); bool transition = live.IsInTransition(0);
            var next = transition ? live.GetNextAnimatorStateInfo(0) : default;
            Need(Finite(current.normalizedTime) && (!transition || Finite(next.normalizedTime)), "finite-selection-phase");
            float Clip(bool incoming, bool farewell)
            {
                clips.Clear(); if (incoming) live.GetNextAnimatorClipInfo(0, clips); else live.GetCurrentAnimatorClipInfo(0, clips);
                Need(clips.Count == 1 && clips[0].clip == (farewell ? takeClip : idleClip)
                    && Finite(clips[0].weight) && clips[0].weight >= 0f && clips[0].weight <= 1f, "exact-selected-clip-object-weight");
                return clips[0].weight;
            }
            Need(current.IsName("Idle") || current.IsName(CharacterPresentation.FarewellState), "no-foreign-current-state");
            float currentWeight = Clip(false, current.IsName(CharacterPresentation.FarewellState)), nextWeight = 0f;
            if (transition)
            {
                Need(current.IsName("Idle") && next.IsName(CharacterPresentation.FarewellState)
                    || current.IsName(CharacterPresentation.FarewellState) && next.IsName("Idle"), "exact-incoming-outgoing-crossing");
                nextWeight = Clip(true, next.IsName(CharacterPresentation.FarewellState));
            }
            else Need(pose.Idle || pose.InTake, "positive-settled-selection");
            return new NativeSelection(pose, current, next, transition, currentWeight, nextWeight);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (session == null || session.finalizing) return;
            if (!warmReady && !session.releasing && Time.frameCount <= boundFrame) return;
            callbacks++;
            if (session.releasing && releaseRefusal != null) return; // One refused cleanup/observation is never retried.
            preMask = targetMask = beforeBoneMask = finalBoneMask = postWeightMask = postGoalMask = 0; selectionRead = false;
            try
            {
                Need(callbacks <= CallbackCap && layerIndex == 0 && !pending && callbackFrame != Time.frameCount, "one-normal-callback-per-frame");
                callbackFrame = Time.frameCount; // Stamp the invocation BEFORE a possibly refused read or partial write.
                preRoot = body != null ? body.transform.position : default;
                preHeading = body != null ? body.transform.rotation : default;
                if (session.releasing) CheckChannelLease(); else CheckIdentity();
                callbackRelease = session.releasing; callbackPlaying = session.playing; callbackAfterRelease = releaseWritten;
                for (int i = 0; i < 2; i++)
                {
                    prePositionWeight[i] = live.GetIKPositionWeight(Goals[i]);
                    preRotationWeight[i] = live.GetIKRotationWeight(Goals[i]);
                    Need(Weight(prePositionWeight[i]) && Weight(preRotationWeight[i]), "finite-foot-channel-weights");
                }
                if (session.releasing) ReleaseInsideCallback(); // Lease + owned weights, BEFORE optional geometry/selection diagnostics.
                for (int i = 0; i < 4; i++)
                { pre[i] = bones[i].position; preRotation[i] = bones[i].rotation;
                    Need(Finite(pre[i]) && Valid(preRotation[i]), "finite-pre-bone"); beforeBoneMask |= 1 << i; }
                for (int i = 0; i < 2; i++)
                {
                    goal[i] = live.GetIKPosition(Goals[i]); goalRotation[i] = live.GetIKRotation(Goals[i]);
                    Need(Finite(goal[i]) && Valid(goalRotation[i]) && Valid(preRotation[i]), "finite-raw-goal-bone"); preMask |= 1 << i;
                }
                callbackSelection = Selection(); callbackPhase = callbackSelection.pose.NormalizedTime; selectionRead = true;
                if (!session.releasing && session.playing)
                {
                    Need(warmReady && cueFired && Time.realtimeSinceStartup - session.takeAt <= TakeSeconds, "bounded-own-take");
                    for (int i = 0; i < 2; i++) ComputeTarget(i); // BOTH candidate geometries precede ANY setter.
                    CheckIdentity(); // Reject root/body/cue/host changes BEFORE engine writes.
                    for (int i = 0; i < 2; i++)
                    {
                        // Some native evaluations may reset prior weights. Record 0/1 rather than assume persistence.
                        Need((ownedMask & (1 << i)) != 0
                            ? BinaryWeight(prePositionWeight[i]) && BinaryWeight(preRotationWeight[i])
                            : prePositionWeight[i] == 0f && preRotationWeight[i] == 0f, "owned-foot-channel-readback");
                    }
                    for (int i = 0; i < 2; i++)
                    {
                        // The fixture owns the reviewed fixed host. A native exception may be partial: stamp BEFORE each attempt.
                        ownedMask |= 1 << i;
                        setterAttempts++; live.SetIKPosition(Goals[i], target[i]);
                        setterAttempts++; live.SetIKRotation(Goals[i], targetRotation[i]);
                        setterAttempts++; live.SetIKPositionWeight(Goals[i], 1f);
                        setterAttempts++; live.SetIKRotationWeight(Goals[i], 1f);
                        ReadPostWeights(i);
                        Need(postPositionWeight[i] == 1f && postRotationWeight[i] == 1f, "active-weight-readback");
                        postGoal[i] = live.GetIKPosition(Goals[i]); postGoalRotation[i] = live.GetIKRotation(Goals[i]);
                        Need(Finite(postGoal[i]) && Valid(postGoalRotation[i]), "finite-post-goal-readback"); postGoalMask |= 1 << i;
                    }
                }
                else if (!session.releasing)
                    Need(prePositionWeight.All(w => w == 0f) && preRotationWeight.All(w => w == 0f), "initial-zero-foot-channels");
                pending = true;
            }
            catch (System.Exception error)
            {
                if (session.releasing && releaseRefusal == null) releaseRefusal = Code("release-callback", error);
                session.Fail(Code("callback", error));
                TryFailureRow("callback"); // Cached actual reads only; no second pose/goal/identity guard.
            }
        }

        private void ComputeTarget(int i)
        {
            var v = pre[i + 2] - pre[i]; var desired = anchors[i + 2] - anchors[i];
            Need(Finite(v) && Finite(desired) && v.sqrMagnitude > 1e-6f && desired.sqrMagnitude > 1e-6f, "nondegenerate-toe-vectors");
            var toeLocal = Quaternion.Inverse(preRotation[i]) * v;
            var atBase = baseRotation[i] * toeLocal;
            Need(Vector3.Dot(atBase.normalized, desired.normalized) > -.999f, "no-ambiguous-halfturn-toe-alignment");
            var desiredFootRotation = Quaternion.FromToRotation(atBase, desired) * baseRotation[i];
            offset[i] = Quaternion.Inverse(goalRotation[i]) * (pre[i] - goal[i]);
            offsetRotation[i] = Quaternion.Inverse(goalRotation[i]) * preRotation[i];
            targetRotation[i] = desiredFootRotation * Quaternion.Inverse(offsetRotation[i]);
            target[i] = anchors[i] - targetRotation[i] * offset[i];
            predictedToe[i] = anchors[i] + desiredFootRotation * toeLocal;
            float predictionGap = Vector3.Distance(predictedToe[i], anchors[i + 2]);
            Need(Finite(offset[i]) && Valid(offsetRotation[i]) && Valid(targetRotation[i]) && Finite(target[i])
                && Finite(predictedToe[i]) && Finite(predictionGap), "finite-dynamic-target"); targetMask |= 1 << i;
            maxPrediction = Mathf.Max(maxPrediction, predictionGap);
            Need(predictionGap <= FootBound, "rigid-toe-target-infeasible");
            bool actualDemand = Vector3.Distance(pre[i], anchors[i]) > FootBound || Vector3.Distance(pre[i + 2], anchors[i + 2]) > FootBound;
            demand |= actualDemand; positiveTakeDemand |= actualDemand && callbackSelection.pose.InTake
                && Positive(callbackSelection.pose.SelectedClipWeight);
        }

        private void ReleaseInsideCallback()
        {
            if (releaseWritten)
            {
                Need(prePositionWeight.All(w => w == 0f) && preRotationWeight.All(w => w == 0f), "later-zero-foot-channels");
                return; // Never re-zero a later owner: there are NO release retries.
            }
            Need(!releaseAttempted, "release-already-refused-no-retry");
            releaseAttempted = true; // Invalidate target-write authority before cleanup.
            for (int i = 0; i < 2; i++)
            {
                if ((ownedMask & (1 << i)) == 0)
                { Need(prePositionWeight[i] == 0f && preRotationWeight[i] == 0f, "unowned-channel-must-be-zero"); continue; }
                // Not inferred from numeric equality alone: exact fixed fixture host/body lease also held above.
                Need(BinaryWeight(prePositionWeight[i]) && BinaryWeight(preRotationWeight[i]), "release-channel-was-replaced");
                setterAttempts++; live.SetIKPositionWeight(Goals[i], 0f);
                setterAttempts++; live.SetIKRotationWeight(Goals[i], 0f);
                ReadPostWeights(i);
                Need(postPositionWeight[i] == 0f && postRotationWeight[i] == 0f, "release-zero-readback");
                ownedMask &= ~(1 << i);
            }
            releaseWritten = true;
        }

        private void ReadPostWeights(int i)
        {
            postPositionWeight[i] = live.GetIKPositionWeight(Goals[i]);
            postRotationWeight[i] = live.GetIKRotationWeight(Goals[i]);
            Need(Weight(postPositionWeight[i]) && Weight(postRotationWeight[i]), "finite-post-weight-readback");
            postWeightMask |= 1 << i;
        }

        private void LateUpdate()
        {
            if (session == null || session.finalizing || !pending) return;
            pending = false;
            try
            {
                Need(callbackFrame == Time.frameCount && lateFrame != Time.frameCount, "actual-same-frame-LateUpdate");
                if (callbackRelease) CheckChannelLease(); else CheckIdentity();
                var selected = Selection(); var pose = selected.pose; var beforePose = callbackSelection.pose;
                Need(pose.InTake == beforePose.InTake && pose.Idle == beforePose.Idle
                    && pose.Boundary == beforePose.Boundary && pose.NormalizedTime == callbackPhase
                    && pose.SelectedClipWeight == beforePose.SelectedClipWeight
                    && selected.currentHash == callbackSelection.currentHash && selected.nextHash == callbackSelection.nextHash
                    && selected.currentPhase == callbackSelection.currentPhase && selected.nextPhase == callbackSelection.nextPhase
                    && selected.currentWeight == callbackSelection.currentWeight && selected.nextWeight == callbackSelection.nextWeight
                    && selected.transition == callbackSelection.transition, "same-native-selection-evaluation");
                for (int i = 0; i < 4; i++)
                { final[i] = bones[i].position; finalRotation[i] = bones[i].rotation;
                    Need(Finite(final[i]) && Valid(finalRotation[i]), "finite-final-bone"); finalBoneMask |= 1 << i; }
                finalRoot = pose.Root; finalHeading = pose.Heading;
                float elapsed = Time.realtimeSinceStartup - (callbackPlaying ? session.takeAt : callbackRelease ? session.releaseAt : session.warmAt);
                Need(Finite(elapsed) && elapsed >= 0f, "finite-natural-stage-clock");
                Need(elapsed <= (callbackPlaying ? TakeSeconds : callbackRelease ? ReleaseSeconds : WarmSeconds), "natural-stage-deadline");
                lateFrame = Time.frameCount; lateReads++;
                if (!warmReady && !session.playing && !session.releasing)
                {
                    Need(pose.Idle && !pose.InTake && pose.Boundary == CharacterPresentation.FarewellBoundary.None, "warm-positive-Idle");
                    Need(Time.frameCount > boundFrame && selected.currentPhase > initialIdlePhase, "new-naturally-advanced-warm-Idle");
                    for (int i = 0; i < 4; i++) anchors[i] = final[i];
                    for (int i = 0; i < 4; i++) baseRotation[i] = finalRotation[i];
                    bodyBaselineHand = pose.RightHand.y;
                    warmReady = true; Record("ANCHOR", true); // The sole baseline capture, never repeated.
                }
                else if (callbackPlaying)
                {
                    for (int i = 0; i < 4; i++) maxFeet = Mathf.Max(maxFeet, Vector3.Distance(anchors[i], final[i]));
                    maxRoot = Mathf.Max(maxRoot, Vector3.Distance(rootAnchor, pose.Root));
                    maxHeading = Mathf.Max(maxHeading, Quaternion.Angle(headingAnchor, pose.Heading));
                    Record("SOLVED", false); // Raw failing coordinates are retained BEFORE the strict assertions.
                    Need(maxFeet <= FootBound, "actual-final-foot-toe-1mm");
                    Need(maxRoot <= RootBound && maxHeading <= HeadingBound, "actual-final-root-heading");
                    if (pose.InTake)
                    {
                        Need(Positive(pose.SelectedClipWeight) && pose.NormalizedTime >= lastTakePhase, "positive-natural-take-phase");
                        lastTakePhase = pose.NormalizedTime; if (enteredFrame < 0) enteredFrame = Time.frameCount;
                        neutral |= pose.NormalizedTime >= CharacterPresentation.FarewellNeutral;
                        if (pose.NormalizedTime >= 10f / 30f && pose.NormalizedTime <= 17f / 30f
                            && pose.RightHand.y > bodyBaselineHand + .03f) palmSamples++;
                    }
                    if (pose.Idle && enteredFrame >= 0 && !completed)
                    { Need(neutral && palmSamples > 0, "actual-neutral-and-palm-before-returned-Idle"); completed = true; Record("RETURNED-IDLE", true); }
                }
                else if (callbackRelease && releaseWritten)
                {
                    Record(callbackAfterRelease ? "AFTER-RELEASE" : "RELEASE", true);
                    if (callbackAfterRelease)
                    {
                        Need(selected.currentHash != releaseHash || selected.currentPhase > releasePhase, "later-natural-phase");
                        laterZeroFrames++;
                    }
                    releasePhase = selected.currentPhase; releaseHash = selected.currentHash;
                }
            }
            catch (System.Exception error)
            {
                if (callbackRelease) releaseRefusal = Code("release-late", error);
                session.Fail(Code("late", error)); TryFailureRow("late");
            }
        }

        private float bodyBaselineHand = float.NegativeInfinity;
        private void Record(string stage, bool force)
        {
            float now = Time.realtimeSinceStartup;
            if (!force && now - lastRow < 1f / 60f) return;
            lastRow = now;
            var row = new System.Text.StringBuilder(2048);
            row.Append("ROW body=").Append(index).Append(" stage=").Append(stage).Append(" callbackFrame=").Append(callbackFrame)
                .Append(" lateFrame=").Append(lateFrame).Append(" callbacks=").Append(callbacks)
                .Append(" elapsedTake=").Append(N(session.takeAt > 0f ? now - session.takeAt : 0f))
                .Append(" preMask=").Append(preMask).Append(" targetMask=").Append(targetMask)
                .Append(" beforeBoneMask=").Append(beforeBoneMask).Append(" finalBoneMask=").Append(finalBoneMask)
                .Append(" postWeightMask=").Append(postWeightMask).Append(" selectionRead=").Append(selectionRead)
                .Append(" postGoalMask=").Append(postGoalMask)
                .Append(" inTake=").Append(callbackSelection.pose.InTake).Append(" idle=").Append(callbackSelection.pose.Idle)
                .Append(" boundary=").Append(callbackSelection.pose.Boundary).Append(" phase=").Append(N(callbackPhase))
                .Append(" selectedWeight=").Append(N(callbackSelection.pose.SelectedClipWeight))
                .Append(" current=").Append(callbackSelection.currentHash).Append('/').Append(N(callbackSelection.currentPhase)).Append('/').Append(N(callbackSelection.currentWeight))
                .Append(" next=").Append(callbackSelection.nextHash).Append('/').Append(N(callbackSelection.nextPhase)).Append('/').Append(N(callbackSelection.nextWeight))
                .Append(" transition=").Append(callbackSelection.transition).Append(" ownedMask=").Append(ownedMask)
                .Append(" anchorsCaptured=").Append(warmReady)
                .Append(" baselineHandY=").Append(warmReady ? N(bodyBaselineHand) : "unavailable")
                .Append(" actualRightHand=").Append(V(callbackSelection.pose.RightHand))
                .Append(" actualHead=").Append(V(callbackSelection.pose.Head))
                .Append(" preRoot=").Append(V(preRoot)).Append(" preHeading=").Append(Q(preHeading))
                .Append(" finalRoot=").Append(V(finalRoot)).Append(" finalHeading=").Append(Q(finalHeading))
                .Append(" maxFeet=").Append(N(maxFeet)).Append(" maxRoot=").Append(N(maxRoot)).Append(" maxHeading=").Append(N(maxHeading));
            for (int i = 0; i < 2; i++) row.Append(" goal").Append(i).Append('=').Append(V(goal[i])).Append('/').Append(Q(goalRotation[i]))
                .Append(" preWeight").Append(i).Append('=').Append(N(prePositionWeight[i])).Append('/').Append(N(preRotationWeight[i]))
                .Append(" postWeight").Append(i).Append('=').Append(N(postPositionWeight[i])).Append('/').Append(N(postRotationWeight[i]))
                .Append(" postGoal").Append(i).Append('=').Append(V(postGoal[i])).Append('/').Append(Q(postGoalRotation[i]))
                .Append(" d").Append(i).Append('=').Append(V(offset[i])).Append(" q").Append(i).Append('=').Append(Q(offsetRotation[i]))
                .Append(" target").Append(i).Append('=').Append(V(target[i])).Append('/').Append(Q(targetRotation[i]))
                .Append(" predictedToe").Append(i).Append('=').Append(V(predictedToe[i]));
            for (int i = 0; i < 4; i++) row.Append(" anchor").Append(i).Append('=').Append(V(anchors[i])).Append('/').Append(Q(baseRotation[i]))
                .Append(" pre").Append(i).Append('=').Append(V(pre[i])).Append('/').Append(Q(preRotation[i]))
                .Append(" final").Append(i).Append('=').Append(V(final[i])).Append('/').Append(Q(finalRotation[i]));
            row.Append("\n"); session.Line(row.ToString(), true);
        }
        private void TryFailureRow(string stage)
        {
            if (releaseAttempted && !releaseWritten && releaseRefusal == null) releaseRefusal = session.failure;
            try { Record("REFUSED-" + stage, true); }
            catch (System.Exception error)
            { session.diagnosticFailure = Code("failure-row", error); session.Fail(session.diagnosticFailure); }
        }
        private static string Code(string stage, System.Exception error) => stage + ":"
            + (error is Refusal refusal ? refusal.code : error.GetType().Name);
        private sealed class Refusal : System.Exception
        { internal readonly string code; internal Refusal(string value) : base("dynamic-ik-A:" + value) { code = value; } }
        private static void Need(bool value, string code) { if (!value) throw new Refusal(code); }
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        private static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        private static bool Valid(Quaternion q) => Finite(q.x) && Finite(q.y) && Finite(q.z) && Finite(q.w)
            && Mathf.Abs(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w - 1f) <= .001f;
        private static bool Weight(float v) => Finite(v) && v >= 0f && v <= 1f;
        private static bool BinaryWeight(float v) => v == 0f || v == 1f;
        private static bool Positive(float v) => Weight(v) && v > 0f;
        private static string N(float v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        private static string V(Vector3 v) => N(v.x) + "," + N(v.y) + "," + N(v.z);
        private static string Q(Quaternion q) => N(q.x) + "," + N(q.y) + "," + N(q.z) + "," + N(q.w);
        private static string Id(Object value) => value == null ? "null" : value.GetEntityId().ToString();
    }

}
#endif
#if UNITY_EDITOR
namespace Gamesim.Tests.PlayMode
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text;
    using Gamesim.Presentation;
    using Gamesim.Simulation;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.Animations;
    using UnityEngine;
    using UnityEngine.TestTools;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Source21's test-private, pre-construction comparison. It changes only the copied Idle state's
    /// Foot IK flag and deliberately retains the Source20 far-park/AlwaysAnimate fixture.
    /// </summary>
    public sealed class Source21ProvidedIdleFootIkPlayModeTests
    {
        private const string SkeletonPath = "Assets/Gamesim/Art/Authored/Animation/Humanoid/bb_anim_PoseAtEase.fbx";
        private const string SourceControllerPath = "Assets/Gamesim/Art/Characters/GamesimHumanoid.controller";
        private const string HandlePath = "Assets/Gamesim/Resources/Animation/GamesimHumanoid.overrideController";
        private const string CandidatePath = "Assets/Gamesim/Tests/PlayMode/__Source21NormalIdleFootIkProvided.controller";
        private const string SourceSha256 = "0a3c362d901f5cd07889b735aa52c38e0b04bc90e904c7ce5d8799e48c6f00b4";
        private const string HandleSha256 = "73893112c0f5bb2188da30eca1d7f8c15d2a01dcb9df835efb544f6f83ea6e6b";
        private const int ExpectedStates = 43, MappingCap = 128, TransitionCap = 256, SubassetCap = 512;

        private readonly List<RuntimeAnimatorController> ownedRuntimeControllers = new List<RuntimeAnimatorController>(4);
        private readonly Source21ProvidedReadyBody[] ready = new Source21ProvidedReadyBody[2];
        private GameObject first, second;
        private CharacterPresentation a, b;
        private Source21ProvidedBodyProvider provider;
        private AnimatorController candidate;
        private AnimatorOverrideController candidateRuntime;
        private AnimationClip expectedIdle;
        private bool providerRegistered, candidateOwned;
        private string sourceHash, handleHash, candidateGuid;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            PrepareCandidate();
            provider = new Source21ProvidedBodyProvider(candidateRuntime);
            CharacterBodySource.Register(provider);
            providerRegistered = true;

            first = new GameObject("Source21 Foot IK provided first");
            second = new GameObject("Source21 Foot IK provided second");
            first.transform.position = new Vector3(1600f, 0f, 1600f);
            second.transform.position = first.transform.position + Vector3.right * 3.34f;
            a = CharacterPresentation.Attach(first, ContentCatalog.Create(91).Find(ContentCatalog.PlayerId), Color.white);
            b = CharacterPresentation.Attach(second, ContentCatalog.Create(92).contestants.First(person => !person.isPlayer), Color.white);
            a.SetReducedMotion(false);
            b.SetReducedMotion(false);

            yield return Source21ProvidedReadiness.Wait(new[] { a, b }, candidateRuntime, expectedIdle,
                AnimatorCullingMode.AlwaysAnimate, 3f, int.MaxValue, ready);
        }

        [UnityTest]
        [Category("FarewellIdleFootIkDiagnostic")]
        public IEnumerator CopiedIdleFootIk_ProvidedSkeletonsKeepFourLandmarksAgainstOneBaseline()
        {
            yield return Source21ProvidedIdleFootIkObserver.Observe(ready,
                "source21-provided-far-always-animate-idle-footik-only");
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            Source21ProvidedIdleFootIkObserver.DisarmAll(ready);
            if (providerRegistered && provider != null)
            {
                CharacterBodySource.Unregister(provider);
                providerRegistered = false;
            }
            if (first != null) Object.Destroy(first);
            if (second != null) Object.Destroy(second);
            foreach (var controller in ownedRuntimeControllers)
                if (controller != null && !EditorUtility.IsPersistent(controller)) Object.Destroy(controller);
            yield return null;

            bool providerReleased = provider == null || CharacterBodySource.Provider != provider;
            bool objectsReleased = first == null && second == null;
            bool wrappersReleased = ownedRuntimeControllers.All(controller => controller == null);
            bool exactLease = true, deleted = true;
            if (candidateOwned)
            {
                var leased = AssetDatabase.LoadMainAssetAtPath(CandidatePath);
                exactLease = leased != null && AssetDatabase.GetAssetPath(leased) == CandidatePath
                    && !string.IsNullOrEmpty(candidateGuid)
                    && AssetDatabase.AssetPathToGUID(CandidatePath) == candidateGuid;
                deleted = exactLease && providerReleased && objectsReleased && wrappersReleased
                    && AssetDatabase.DeleteAsset(CandidatePath);
                candidateOwned = false;
            }
                Assert.That(exactLease, Is.True, "SOURCE21_CLEANUP_ASSET_LEASE");
                Assert.That(deleted, Is.True, "SOURCE21_CLEANUP_DELETE");
                Assert.That(providerReleased, Is.True, "SOURCE21_CLEANUP_PROVIDER_RELEASED");
                Assert.That(objectsReleased, Is.True, "SOURCE21_CLEANUP_OBJECTS_RELEASED");
                Assert.That(wrappersReleased, Is.True, "SOURCE21_CLEANUP_WRAPPERS_RELEASED");
                Assert.That(AssetDatabase.LoadMainAssetAtPath(CandidatePath), Is.Null, "SOURCE21_CLEANUP_ASSET_ABSENT");
                Assert.That(File.Exists(Absolute(CandidatePath)), Is.False, "SOURCE21_CLEANUP_FILE_ABSENT");
                Assert.That(File.Exists(Absolute(CandidatePath + ".meta")), Is.False, "SOURCE21_CLEANUP_META_ABSENT");
                if (sourceHash != null) Assert.That(HashAsset(SourceControllerPath), Is.EqualTo(sourceHash), "SOURCE21_SOURCE_HASH_CHANGED");
                if (handleHash != null) Assert.That(HashAsset(HandlePath), Is.EqualTo(handleHash), "SOURCE21_HANDLE_HASH_CHANGED");
        }

        private void PrepareCandidate()
        {
            Assert.That(AssetDatabase.LoadMainAssetAtPath(CandidatePath), Is.Null, "SOURCE21_PREEXISTING_CANDIDATE_ASSET");
            Assert.That(File.Exists(Absolute(CandidatePath)), Is.False, "SOURCE21_PREEXISTING_CANDIDATE_FILE");
            Assert.That(File.Exists(Absolute(CandidatePath + ".meta")), Is.False, "SOURCE21_PREEXISTING_CANDIDATE_META");

            sourceHash = HashAsset(SourceControllerPath);
            handleHash = HashAsset(HandlePath);
            Assert.That(sourceHash, Is.EqualTo(SourceSha256), "SOURCE21_SOURCE_PIN");
            Assert.That(handleHash, Is.EqualTo(HandleSha256), "SOURCE21_HANDLE_PIN");

            var source = AssetDatabase.LoadAssetAtPath<AnimatorController>(SourceControllerPath);
            var handle = Resources.Load<AnimatorOverrideController>("Animation/GamesimHumanoid");
            Assert.That(source, Is.Not.Null, "SOURCE21_SOURCE_CONTROLLER");
            Assert.That(handle, Is.Not.Null, "SOURCE21_RESOURCES_HANDLE");
            Assert.That(AssetDatabase.GetAssetPath(handle), Is.EqualTo(HandlePath));
            Assert.That(handle.runtimeAnimatorController, Is.SameAs(source), "SOURCE21_RESOURCES_BASE");

            Assert.That(AssetDatabase.CopyAsset(SourceControllerPath, CandidatePath), Is.True, "SOURCE21_COPY_ASSET");
            candidateOwned = true;
            candidateGuid = AssetDatabase.AssetPathToGUID(CandidatePath);
            Assert.That(string.IsNullOrEmpty(candidateGuid), Is.False, "SOURCE21_CANDIDATE_GUID");
            candidate = AssetDatabase.LoadAssetAtPath<AnimatorController>(CandidatePath);
            Assert.That(candidate, Is.Not.Null, "SOURCE21_COPIED_CONTROLLER");
            VerifyGraph(source, candidate, false);

            var idle = State(candidate, "Idle");
            idle.iKOnFeet = true;
            EditorUtility.SetDirty(idle);
            EditorUtility.SetDirty(candidate);
            AssetDatabase.SaveAssetIfDirty(candidate);
            AssetDatabase.ImportAsset(CandidatePath, ImportAssetOptions.ForceSynchronousImport);

            candidate = AssetDatabase.LoadAssetAtPath<AnimatorController>(CandidatePath);
            Assert.That(AssetDatabase.AssetPathToGUID(CandidatePath), Is.EqualTo(candidateGuid), "SOURCE21_CANDIDATE_GUID_CHANGED");
            VerifyGraph(source, candidate, true);
            candidateRuntime = new AnimatorOverrideController(candidate) { name = "Source21 provided Idle Foot IK" };
            ownedRuntimeControllers.Add(candidateRuntime);
            CopyAndVerifyOverrides(handle, candidateRuntime);
            expectedIdle = Effective(candidateRuntime, (AnimationClip)State(candidate, "Idle").motion);
            Assert.That(expectedIdle, Is.SameAs(Effective(handle, (AnimationClip)State(source, "Idle").motion)),
                "SOURCE21_EFFECTIVE_IDLE_CHANGED");
            Assert.That(expectedIdle != null && expectedIdle.humanMotion && expectedIdle.isLooping
                && Finite(expectedIdle.length) && expectedIdle.length > 0f, Is.True, "SOURCE21_EFFECTIVE_IDLE_INVALID");
            Assert.That(HashAsset(SourceControllerPath), Is.EqualTo(sourceHash), "SOURCE21_SOURCE_DIRTIED");
            Assert.That(HashAsset(HandlePath), Is.EqualTo(handleHash), "SOURCE21_HANDLE_DIRTIED");
        }

        private static void CopyAndVerifyOverrides(AnimatorOverrideController source, AnimatorOverrideController copy)
        {
            Assert.That(source.overridesCount, Is.LessThanOrEqualTo(MappingCap), "SOURCE21_OVERRIDE_CAP");
            var sourceRows = new List<KeyValuePair<AnimationClip, AnimationClip>>(source.overridesCount);
            source.GetOverrides(sourceRows);
            Assert.That(sourceRows.Count, Is.EqualTo(source.overridesCount), "SOURCE21_OVERRIDE_SOURCE_COUNT");
            var keys = new HashSet<AnimationClip>();
            foreach (var row in sourceRows)
            {
                Assert.That(row.Key, Is.Not.Null, "SOURCE21_OVERRIDE_KEY_NULL");
                Assert.That(keys.Add(row.Key), Is.True, "SOURCE21_OVERRIDE_KEY_DUPLICATE");
            }
            copy.ApplyOverrides(sourceRows);
            Assert.That(copy.overridesCount, Is.EqualTo(sourceRows.Count), "SOURCE21_OVERRIDE_COPY_ADMISSION");
            Assert.That(copy.overridesCount, Is.LessThanOrEqualTo(MappingCap), "SOURCE21_OVERRIDE_COPY_CAP");
            var copiedRows = new List<KeyValuePair<AnimationClip, AnimationClip>>(copy.overridesCount);
            copy.GetOverrides(copiedRows);
            Assert.That(copiedRows.Count, Is.EqualTo(sourceRows.Count), "SOURCE21_OVERRIDE_COUNT");
            var proof = new BoundedProof();
            proof.Append("SOURCE21-OVERRIDES version=1 fixture=provided count=").Append(sourceRows.Count).Append('\n');
            for (int i = 0; i < sourceRows.Count; i++)
            {
                Assert.That(copiedRows[i].Key, Is.SameAs(sourceRows[i].Key), "SOURCE21_OVERRIDE_KEY_" + i);
                Assert.That(copiedRows[i].Value, Is.SameAs(sourceRows[i].Value), "SOURCE21_OVERRIDE_VALUE_" + i);
                var effective = sourceRows[i].Value != null ? sourceRows[i].Value : sourceRows[i].Key;
                var keyName = BoundedProof.Name(sourceRows[i].Key.name, "SOURCE21_OVERRIDE_KEY_NAME_CAP_" + i);
                var effectiveName = BoundedProof.Name(effective.name, "SOURCE21_OVERRIDE_EFFECTIVE_NAME_CAP_" + i);
                proof.Append("MAP ordinal=").Append(i).Append(" key=").Append(keyName)
                    .Append('#').Append(sourceRows[i].Key.GetEntityId().ToString()).Append(" effective=")
                    .Append(effectiveName).Append('#').Append(effective.GetEntityId().ToString()).Append('\n');
            }
            Debug.Log(proof.Finish());
        }

        private static AnimationClip Effective(AnimatorOverrideController controller, AnimationClip original)
        {
            Assert.That(original, Is.Not.Null);
            var replacement = controller[original];
            return replacement != null ? replacement : original;
        }

        private static void VerifyGraph(AnimatorController source, AnimatorController copy, bool idleEnabled)
        {
            Assert.That(source.layers.Length, Is.EqualTo(1));
            Assert.That(copy.layers.Length, Is.EqualTo(1));
            var sl = source.layers[0];
            var cl = copy.layers[0];
            Assert.That(sl.iKPass && cl.iKPass, Is.True, "SOURCE21_BASE_IK_PASS");
            Assert.That(cl.name, Is.EqualTo(sl.name));
            Assert.That(cl.defaultWeight, Is.EqualTo(sl.defaultWeight));
            Assert.That(cl.blendingMode, Is.EqualTo(sl.blendingMode));
            Assert.That(cl.syncedLayerIndex, Is.EqualTo(sl.syncedLayerIndex));
            Assert.That(cl.syncedLayerAffectsTiming, Is.EqualTo(sl.syncedLayerAffectsTiming));
            Assert.That(cl.avatarMask, Is.SameAs(sl.avatarMask));
            Assert.That(sl.stateMachine.stateMachines.Length, Is.Zero, "SOURCE21_SOURCE_NESTED_MACHINE");
            Assert.That(cl.stateMachine.stateMachines.Length, Is.Zero, "SOURCE21_COPY_NESTED_MACHINE");
            Assert.That(sl.stateMachine.entryTransitions.Length, Is.Zero, "SOURCE21_SOURCE_ENTRY_TRANSITIONS");
            Assert.That(cl.stateMachine.entryTransitions.Length, Is.Zero, "SOURCE21_COPY_ENTRY_TRANSITIONS");
            Assert.That(sl.stateMachine.behaviours.Length, Is.Zero, "SOURCE21_SOURCE_MACHINE_BEHAVIOURS");
            Assert.That(cl.stateMachine.behaviours.Length, Is.Zero, "SOURCE21_COPY_MACHINE_BEHAVIOURS");
            Assert.That(cl.stateMachine.name, Is.EqualTo(sl.stateMachine.name));
            Assert.That((cl.stateMachine.anyStatePosition, cl.stateMachine.entryPosition,
                    cl.stateMachine.exitPosition, cl.stateMachine.parentStateMachinePosition),
                Is.EqualTo((sl.stateMachine.anyStatePosition, sl.stateMachine.entryPosition,
                    sl.stateMachine.exitPosition, sl.stateMachine.parentStateMachinePosition)), "SOURCE21_MACHINE_LAYOUT");
            Assert.That(AssetDatabase.GetAssetPath(cl.stateMachine), Is.EqualTo(CandidatePath));

            var ss = sl.stateMachine.states;
            var cs = cl.stateMachine.states;
            Assert.That(ss.Length, Is.EqualTo(ExpectedStates), "SOURCE21_SOURCE_STATE_COUNT");
            Assert.That(cs.Length, Is.EqualTo(ExpectedStates), "SOURCE21_COPY_STATE_COUNT");
            Assert.That(sl.stateMachine.defaultState, Is.SameAs(State(source, "Idle")));
            Assert.That(cl.stateMachine.defaultState, Is.SameAs(State(copy, "Idle")));
            Assert.That(source.parameters.Length, Is.LessThanOrEqualTo(MappingCap));
            Assert.That(copy.parameters.Length, Is.EqualTo(source.parameters.Length));
            for (int i = 0; i < source.parameters.Length; i++)
            {
                var a = source.parameters[i]; var b = copy.parameters[i];
                Assert.That((b.name, b.type, b.defaultBool, b.defaultInt, b.defaultFloat),
                    Is.EqualTo((a.name, a.type, a.defaultBool, a.defaultInt, a.defaultFloat)), "SOURCE21_PARAMETER_" + i);
            }

            int transitions = 0;
            var names = new HashSet<string>(StringComparer.Ordinal);
            var owned = new HashSet<Object> { copy, cl.stateMachine };
            var proof = new BoundedProof();
            proof.Append("SOURCE21-GRAPH version=1 candidate=").Append(CandidatePath)
                .Append(" idleFootIk=").Append(idleEnabled ? "true" : "false").Append('\n');
            for (int i = 0; i < ss.Length; i++)
            {
                var a = ss[i].state; var b = cs[i].state;
                Assert.That(a, Is.Not.Null); Assert.That(b, Is.Not.Null);
                var sourceName = BoundedProof.Name(a.name, "SOURCE21_SOURCE_STATE_NAME_CAP_" + i);
                var copyName = BoundedProof.Name(b.name, "SOURCE21_COPY_STATE_NAME_CAP_" + i);
                Assert.That(names.Add(sourceName), Is.True, "SOURCE21_DUPLICATE_STATE_" + sourceName);
                Assert.That(a.iKOnFeet, Is.False, "SOURCE21_SOURCE_FOOT_IK_" + sourceName);
                Assert.That(b.iKOnFeet, Is.EqualTo(idleEnabled && copyName == "Idle"), "SOURCE21_COPY_FOOT_IK_" + copyName);
                Assert.That(AssetDatabase.GetAssetPath(b), Is.EqualTo(CandidatePath), "SOURCE21_STATE_OWNERSHIP_" + copyName);
                Assert.That(owned.Add(b), Is.True, "SOURCE21_STATE_SUBASSET_DUPLICATE_" + copyName);
                Assert.That(copyName, Is.EqualTo(sourceName));
                Assert.That(b.motion, Is.SameAs(a.motion), "SOURCE21_MOTION_" + copyName);
                Assert.That(b.motion, Is.TypeOf<AnimationClip>(), "SOURCE21_DIRECT_CLIP_" + copyName);
                Assert.That(cs[i].position, Is.EqualTo(ss[i].position));
                Assert.That((b.speed, b.cycleOffset, b.mirror, b.writeDefaultValues, b.tag),
                    Is.EqualTo((a.speed, a.cycleOffset, a.mirror, a.writeDefaultValues, a.tag)), "SOURCE21_STATE_VALUES_" + copyName);
                Assert.That((b.speedParameterActive, b.speedParameter, b.timeParameterActive, b.timeParameter,
                        b.mirrorParameterActive, b.mirrorParameter, b.cycleOffsetParameterActive, b.cycleOffsetParameter),
                    Is.EqualTo((a.speedParameterActive, a.speedParameter, a.timeParameterActive, a.timeParameter,
                        a.mirrorParameterActive, a.mirrorParameter, a.cycleOffsetParameterActive, a.cycleOffsetParameter)),
                    "SOURCE21_STATE_PARAMETERS_" + copyName);
                Assert.That(b.behaviours.Length, Is.EqualTo(a.behaviours.Length), "SOURCE21_BEHAVIOURS_" + copyName);
                for (int behaviour = 0; behaviour < b.behaviours.Length; behaviour++)
                {
                    Assert.That(b.behaviours[behaviour].GetType(), Is.EqualTo(a.behaviours[behaviour].GetType()));
                    Assert.That(AssetDatabase.GetAssetPath(b.behaviours[behaviour]), Is.EqualTo(CandidatePath));
                    Assert.That(owned.Add(b.behaviours[behaviour]), Is.True);
                }
                var motion = (AnimationClip)b.motion;
                var motionName = BoundedProof.Name(motion.name, "SOURCE21_MOTION_NAME_CAP_" + i);
                proof.Append("STATE ordinal=").Append(i).Append(" name=").Append(copyName)
                    .Append(" sourceStateId=").Append(a.GetEntityId().ToString()).Append(" copyStateId=").Append(b.GetEntityId().ToString())
                    .Append(" motion=").Append(motionName).Append('#').Append(motion.GetEntityId().ToString())
                    .Append(" sourceFootIk=false copyFootIk=").Append(b.iKOnFeet ? "true" : "false").Append('\n');
                AssertTransitions(a.transitions, b.transitions, ss, cs, owned, ref transitions, copyName);
            }
            AssertTransitions(sl.stateMachine.anyStateTransitions, cl.stateMachine.anyStateTransitions,
                ss, cs, owned, ref transitions, "AnyState");
            Assert.That(transitions, Is.LessThanOrEqualTo(TransitionCap), "SOURCE21_TRANSITION_CAP");

            var subassets = AssetDatabase.LoadAllAssetsAtPath(CandidatePath);
            Assert.That(subassets.Length, Is.LessThanOrEqualTo(SubassetCap), "SOURCE21_SUBASSET_CAP");
            var loaded = new HashSet<Object>();
            foreach (var asset in subassets)
            {
                Assert.That(asset, Is.Not.Null, "SOURCE21_SUBASSET_NULL");
                Assert.That(AssetDatabase.GetAssetPath(asset), Is.EqualTo(CandidatePath), "SOURCE21_SUBASSET_OWNERSHIP");
                Assert.That(loaded.Add(asset), Is.True, "SOURCE21_SUBASSET_DUPLICATE");
            }
            Assert.That(loaded.SetEquals(owned), Is.True, "SOURCE21_SUBASSET_SET");
            if (idleEnabled) Debug.Log(proof.Finish());
        }

        private static void AssertTransitions(AnimatorStateTransition[] source, AnimatorStateTransition[] copy,
            ChildAnimatorState[] sourceStates, ChildAnimatorState[] copyStates, HashSet<Object> owned,
            ref int total, string owner)
        {
            Assert.That(copy.Length, Is.EqualTo(source.Length), "SOURCE21_TRANSITIONS_" + owner);
            total = checked(total + source.Length);
            Assert.That(total, Is.LessThanOrEqualTo(TransitionCap), "SOURCE21_TRANSITION_CAP_" + owner);
            for (int i = 0; i < source.Length; i++)
            {
                var a = source[i]; var b = copy[i];
                Assert.That(AssetDatabase.GetAssetPath(b), Is.EqualTo(CandidatePath));
                Assert.That(owned.Add(b), Is.True, "SOURCE21_TRANSITION_SUBASSET_DUPLICATE");
                Assert.That(b.name, Is.EqualTo(a.name));
                int sourceDestination = StateIndex(sourceStates, a.destinationState);
                int copyDestination = StateIndex(copyStates, b.destinationState);
                Assert.That(copyDestination, Is.EqualTo(sourceDestination), "SOURCE21_TRANSITION_DESTINATION_" + owner + "_" + i);
                if (sourceDestination < 0) Assert.That(b.destinationState, Is.Null);
                else Assert.That(b.destinationState, Is.SameAs(copyStates[sourceDestination].state));
                Assert.That(a.destinationStateMachine, Is.Null, "SOURCE21_SOURCE_NESTED_DESTINATION");
                Assert.That(b.destinationStateMachine, Is.Null, "SOURCE21_COPY_NESTED_DESTINATION");
                Assert.That((b.isExit, b.hasExitTime, b.hasFixedDuration, b.exitTime, b.duration, b.offset,
                        b.interruptionSource, b.orderedInterruption, b.canTransitionToSelf, b.mute, b.solo),
                    Is.EqualTo((a.isExit, a.hasExitTime, a.hasFixedDuration, a.exitTime, a.duration, a.offset,
                        a.interruptionSource, a.orderedInterruption, a.canTransitionToSelf, a.mute, a.solo)),
                    "SOURCE21_TRANSITION_VALUES_" + owner + "_" + i);
                Assert.That(b.conditions.Length, Is.EqualTo(a.conditions.Length));
                for (int c = 0; c < a.conditions.Length; c++)
                    Assert.That((b.conditions[c].mode, b.conditions[c].parameter, b.conditions[c].threshold),
                        Is.EqualTo((a.conditions[c].mode, a.conditions[c].parameter, a.conditions[c].threshold)));
            }
        }

        private static int StateIndex(ChildAnimatorState[] states, AnimatorState state)
        {
            if (state == null) return -1;
            for (int i = 0; i < states.Length; i++)
                if (states[i].state == state) return i;
            Assert.Fail("SOURCE21_TRANSITION_DESTINATION_OUTSIDE_ROOT");
            return -2;
        }

        private static AnimatorState State(AnimatorController controller, string name) =>
            controller.layers[0].stateMachine.states.Select(child => child.state).Single(state => state.name == name);

        private static string Absolute(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..",
            assetPath.Replace('/', Path.DirectorySeparatorChar)));

        private static string HashAsset(string assetPath)
        {
            using (var algorithm = SHA256.Create())
            using (var stream = File.OpenRead(Absolute(assetPath)))
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private sealed class BoundedProof
        {
            private const int MaxBytes = 64 * 1024, MaxNameBytes = 256;
            private readonly StringBuilder text = new StringBuilder(8192);
            private int bytes;

            internal BoundedProof Append(string value)
            {
                Assert.That(value, Is.Not.Null, "SOURCE21_PROOF_SEGMENT_NULL");
                int added = Encoding.UTF8.GetByteCount(value);
                Assert.That(added, Is.LessThanOrEqualTo(MaxBytes - bytes), "SOURCE21_PROOF_CAP");
                text.Append(value); bytes += added;
                return this;
            }

            internal BoundedProof Append(char value) => Append(value.ToString());
            internal BoundedProof Append(int value) => Append(value.ToString(CultureInfo.InvariantCulture));
            internal string Finish() => text.ToString();

            internal static string Name(string value, string code)
            {
                Assert.That(value, Is.Not.Null, code);
                Assert.That(Encoding.UTF8.GetByteCount(value), Is.LessThanOrEqualTo(MaxNameBytes), code);
                return value;
            }
        }

        private sealed class Source21ProvidedBodyProvider : IModularCharacterBodyProvider
        {
            private readonly RuntimeAnimatorController controller;
            internal Source21ProvidedBodyProvider(RuntimeAnimatorController value) { controller = value; }
            public ICharacterAppearanceCatalog Catalog => null;

            public bool TryCreate(in CharacterBodyRequest request, Transform parent, Color badge, out CharacterBody body)
            {
                body = default;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonPath);
                var avatar = AssetDatabase.LoadAllAssetsAtPath(SkeletonPath).OfType<Avatar>().FirstOrDefault();
                if (model == null || avatar == null || controller == null) return false;
                var root = Object.Instantiate(model, parent, false);
                var animator = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.Normal;
                body = new CharacterBody(root, animator, false, this);
                return true;
            }

            public bool TryCreate(string id, Transform parent, Color colour, out CharacterBody body)
            { body = default; return false; }
            public void SetWardrobeColor(in CharacterBody body, Color colour) { }
        }
    }

    internal sealed class Source21ProvidedReadyBody
    {
        internal CharacterPresentation body;
        internal Animator animator;
        internal RuntimeAnimatorController controller;
        internal AnimationClip idle;
        internal string characterId;
        internal object identity;
        internal MethodInfo holds;
        internal float phase;
    }

    internal static class Source21ProvidedReadiness
    {
        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly HumanBodyBones[] Bones =
            { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes };

        internal static IEnumerator Wait(CharacterPresentation[] bodies, RuntimeAnimatorController controller,
            AnimationClip idle, AnimatorCullingMode culling, float seconds, int frameCap, Source21ProvidedReadyBody[] result)
        {
            var capture = typeof(CharacterPresentation).GetMethod("TryGetFarewellBody", Flags);
            var holds = typeof(CharacterPresentation).GetMethod("HoldsFarewellBody", Flags);
            Assert.That(capture, Is.Not.Null); Assert.That(holds, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + seconds;
            int frames = 0;
            var startPhase = new float[2];
            var captured = new bool[2];
            while (Time.realtimeSinceStartup < deadline && frames++ < frameCap)
            {
                bool all = true;
                for (int i = 0; i < 2; i++)
                {
                    if (!captured[i])
                    {
                        if (!TryCapture(bodies[i], controller, idle, culling, capture, holds, out result[i]))
                        { all = false; continue; }
                        startPhase[i] = result[i].phase;
                        captured[i] = true;
                        all = false;
                        continue;
                    }
                    Assert.That((bool)holds.Invoke(bodies[i], new[] { result[i].identity }), Is.True,
                        "SOURCE21_READINESS_IDENTITY_CHANGED_" + i);
                    if (!TrySelection(result[i].animator, idle, culling, out float phase) || phase <= startPhase[i]) all = false;
                    else result[i].phase = phase;
                }
                if (all) yield break;
                yield return null;
            }
            Assert.Fail("SOURCE21_READINESS_TIMEOUT");
        }

        private static bool TryCapture(CharacterPresentation body, RuntimeAnimatorController controller, AnimationClip idle,
            AnimatorCullingMode culling, MethodInfo capture, MethodInfo holds, out Source21ProvidedReadyBody ready)
        {
            ready = null;
            if (body == null || body.IsPrimitiveBody || body.IsBodyAssembling || body.IsChangingOutfit) return false;
            var args = new object[] { null };
            if (!(bool)capture.Invoke(body, args) || args[0] == null) return false;
            var identityType = args[0].GetType();
            var animatorField = identityType.GetField("Animator", Flags);
            if (animatorField == null) return false;
            var animator = animatorField.GetValue(args[0]) as Animator;
            var selected = body.VisualRoot.GetComponentInChildren<Animator>(true);
            if (animator == null || selected != animator || animator.runtimeAnimatorController != controller || !animator.isActiveAndEnabled
                || !animator.isInitialized || !animator.isHuman || animator.avatar == null || !animator.avatar.isValid
                || animator.applyRootMotion || animator.speed != 1f || animator.updateMode != AnimatorUpdateMode.Normal
                || animator.cullingMode != culling || animator.layerCount != 1) return false;
            if (!TrySelection(animator, idle, culling, out float phase)) return false;
            foreach (var bone in Bones) if (animator.GetBoneTransform(bone) == null) return false;
            ready = new Source21ProvidedReadyBody
            {
                body = body, animator = animator, controller = controller, idle = idle,
                characterId = body.CharacterId, identity = args[0], holds = holds, phase = phase
            };
            return true;
        }

        private static bool TrySelection(Animator animator, AnimationClip idle, AnimatorCullingMode culling, out float phase)
        {
            phase = 0f;
            if (animator == null || animator.cullingMode != culling || animator.IsInTransition(0)) return false;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            var clips = animator.GetCurrentAnimatorClipInfo(0);
            if (!state.IsName("Idle") || clips.Length != 1 || clips[0].clip != idle || !Finite(clips[0].weight)
                || clips[0].weight <= 0f || clips[0].weight > 1f || !Finite(state.normalizedTime) || state.normalizedTime < 0f) return false;
            phase = state.normalizedTime;
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Read-only Source21 observer. It owns no IK channel and never writes animation state.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class Source21ProvidedIdleFootIkObserver : MonoBehaviour
    {
        private const float Window = 2f, RowInterval = 1f / 60f;
        private const int CallbackCap = 32768, HostCap = 16;
        private static readonly HumanBodyBones[] BoneKinds =
            { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes };
        private static readonly AvatarIKGoal[] Goals = { AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot };

        private readonly Transform[] bones = new Transform[4];
        private readonly Vector3[] pose = new Vector3[4], candidatePose = new Vector3[4], baselinePose = new Vector3[4];
        private readonly float[] landmarkDrift = new float[4];
        private readonly float[] maxLandmarkDrift = new float[4];
        private readonly Vector3[] goalPosition = new Vector3[2];
        private readonly Quaternion[] goalRotation = new Quaternion[2];
        private readonly float[] positionWeight = new float[2], rotationWeight = new float[2];
        private readonly float[] minPositionWeight = { float.PositiveInfinity, float.PositiveInfinity };
        private readonly float[] maxPositionWeight = { float.NegativeInfinity, float.NegativeInfinity };
        private readonly float[] minRotationWeight = { float.PositiveInfinity, float.PositiveInfinity };
        private readonly float[] maxRotationWeight = { float.NegativeInfinity, float.NegativeInfinity };
        private readonly bool[] nonzeroPositionWeight = new bool[2], nonzeroRotationWeight = new bool[2];
        private readonly int[] goalSamples = new int[2];
        private readonly List<AnimatorClipInfo> clips = new List<AnimatorClipInfo>(2);
        private readonly List<MonoBehaviour> currentHosts = new List<MonoBehaviour>(HostCap);
        private MonoBehaviour[] hostBehaviours;
        private bool[] hostEnabled, hostActive;
        private Source21ProvidedReadyBody ready;
        private Session session;
        private CharacterPresentation body;
        private Animator animator;
        private object identity;
        private int index, stateHash, armedFrame, callbackFrame = -1, lateFrame = -1, candidateFrame = -1;
        private int callbacks, completedLate, samples, endpointFrame = -1;
        private float initialPhase, baselinePhase, lastPhase, phase, callbackElapsed, endpointElapsed = -1f;
        private float lastRow = float.NegativeInfinity;
        private Vector3 root, candidateRoot, baselineRoot;
        private Quaternion heading, candidateHeading, baselineHeading;
        private BodyFrame bodyFrame;
        private AnimatorCullingMode culling;
        private CharacterPresentation.Reaction? reaction;
        private CharacterPresentation.Gesture? gesture;
        private CharacterPresentation.BodyActivity activity;
        private float timeScale, maxFootDrift, maxRootDrift, maxHeadingDrift;
        private bool armed, pending, baseline, complete;

        internal static void DisarmAll(Source21ProvidedReadyBody[] bodies)
        {
            if (bodies == null) return;
            foreach (var item in bodies)
                if (item?.animator != null)
                    foreach (var observer in item.animator.GetComponents<Source21ProvidedIdleFootIkObserver>())
                    { observer.armed = false; observer.enabled = false; }
        }

        internal static IEnumerator Observe(Source21ProvidedReadyBody[] bodies, string provenance)
        {
            var session = new Session(provenance);
            var readers = new Source21ProvidedIdleFootIkObserver[2];
            try
            {
                Assert.That(bodies, Is.Not.Null); Assert.That(bodies.Length, Is.EqualTo(2));
                for (int i = 0; i < 2; i++)
                {
                    Assert.That(bodies[i]?.animator, Is.Not.Null);
                    Assert.That(bodies[i].animator.GetComponents<Source21ProvidedIdleFootIkObserver>(), Is.Empty,
                        "SOURCE21_FRESH_OBSERVER_HOST_" + i);
                    readers[i] = bodies[i].animator.gameObject.AddComponent<Source21ProvidedIdleFootIkObserver>();
                    readers[i].Bind(bodies[i], i, session);
                    session.Register(i, readers[i]);
                }
                int frame = Time.frameCount;
                float now = Time.realtimeSinceStartup;
                foreach (var reader in readers) reader.Arm(frame, now);
                float deadline = now + 8f;
                while (!session.Done && session.Failure == null && Time.realtimeSinceStartup < deadline) yield return null;
                if (!session.Done && session.Failure == null) session.Fail(-1, "OBSERVATION_TIMEOUT");
            }
            finally
            {
                foreach (var reader in readers)
                    if (reader != null) { reader.armed = false; reader.enabled = false; }
                Debug.Log(session.Finish(readers));
                foreach (var reader in readers) if (reader != null) Object.Destroy(reader);
            }
            Assert.That(session.Failure, Is.Null, session.Failure);
            foreach (var reader in readers)
            {
                Assert.That(reader.baseline, Is.True, "SOURCE21_BASELINE_MISSING");
                Assert.That(reader.complete, Is.True, "SOURCE21_ENDPOINT_MISSING");
                Assert.That(reader.samples, Is.GreaterThan(0), "SOURCE21_NO_WINDOW_SAMPLES");
                Assert.That(reader.baselinePhase, Is.GreaterThan(reader.initialPhase), "SOURCE21_BASELINE_DID_NOT_ADVANCE");
                Assert.That(reader.phase, Is.GreaterThan(reader.baselinePhase), "SOURCE21_WINDOW_IDLE_DID_NOT_ADVANCE");
                Assert.That(reader.endpointFrame, Is.GreaterThan(session.BaselineFrame));
                Assert.That(reader.endpointElapsed, Is.GreaterThanOrEqualTo(Window), "SOURCE21_ENDPOINT_BEFORE_WINDOW");
            }
        }

        private void Bind(Source21ProvidedReadyBody value, int bodyIndex, Session owner)
        {
            ready = value; session = owner; index = bodyIndex; body = value.body; animator = value.animator; identity = value.identity;
            if (!Need(body != null && animator != null && identity != null, "BIND_IDENTITY")) return;
            if (!Need((bool)value.holds.Invoke(body, new[] { identity }), "BIND_HELD")) return;
            if (!Need(body.IsStill && !body.IsSeated && !body.IsTalking && !body.IsArguing && !body.IsReacting,
                "BIND_STATIC_IDLE")) return;
            for (int i = 0; i < bones.Length; i++)
            { bones[i] = animator.GetBoneTransform(BoneKinds[i]); if (!Need(bones[i] != null, "BIND_BONE_" + i)) return; }
            var state = animator.GetCurrentAnimatorStateInfo(0);
            initialPhase = lastPhase = state.normalizedTime; stateHash = state.fullPathHash;
            Need(Finite(initialPhase) && initialPhase >= 0f && state.IsName("Idle"), "BIND_IDLE");
            bodyFrame = body.Frame; culling = animator.cullingMode; reaction = body.LastReaction; gesture = body.LastGesture;
            activity = body.Activity; timeScale = Time.timeScale;
            Need(culling == AnimatorCullingMode.AlwaysAnimate && Finite(timeScale) && timeScale > 0f, "BIND_RUNTIME");
            CaptureHosts();
        }

        private void CaptureHosts()
        {
            hostBehaviours = animator.gameObject.GetComponents<MonoBehaviour>();
            if (!Need(hostBehaviours.Length <= HostCap, "HOST_CAP")) return;
            hostEnabled = new bool[hostBehaviours.Length]; hostActive = new bool[hostBehaviours.Length];
            for (int i = 0; i < hostBehaviours.Length; i++)
            {
                var host = hostBehaviours[i]; if (!Need(host != null, "HOST_NULL")) return;
                hostEnabled[i] = host.enabled; hostActive[i] = host.isActiveAndEnabled;
                int callbacksFound = 0, depth = 0;
                for (var type = host.GetType(); type != null && typeof(MonoBehaviour).IsAssignableFrom(type); type = type.BaseType)
                {
                    if (!Need(++depth <= 16, "HOST_INHERITANCE_CAP")) return;
                    foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (method.Name != "OnAnimatorIK") continue;
                        if (!Need(++callbacksFound <= 16, "HOST_CALLBACK_CAP")) return;
                    }
                }
                session.Line("HOST body=" + index + " ordinal=" + i + " type=" + host.GetType().FullName
                    + " enabled=" + Bool(hostEnabled[i]) + " active=" + Bool(hostActive[i])
                    + " callbackMethods=" + callbacksFound + " fixtureReader=" + Bool(host == this)
                    + " foreignOwnership=" + (host == this ? "not-applicable" : "unproven") + "\n", false);
            }
        }

        private void Arm(int frame, float now)
        { armedFrame = frame; session.ArmTime = now; armed = true; }

        private void OnAnimatorIK(int layerIndex)
        {
            if (!armed || complete || session == null || session.Failure != null) return;
            if (++callbacks > CallbackCap) { session.Fail(index, "CALLBACK_CAP"); return; }
            if (!Need(layerIndex == 0 && !pending && callbackFrame != Time.frameCount, "CALLBACK_PAIRING")) return;
            for (int i = 0; i < Goals.Length; i++)
            {
                goalPosition[i] = animator.GetIKPosition(Goals[i]);
                goalRotation[i] = animator.GetIKRotation(Goals[i]);
                positionWeight[i] = animator.GetIKPositionWeight(Goals[i]);
                rotationWeight[i] = animator.GetIKRotationWeight(Goals[i]);
                if (!Need(Finite(goalPosition[i]) && Finite(goalRotation[i]) && Weight(positionWeight[i]) && Weight(rotationWeight[i]),
                    "CALLBACK_NONFINITE_" + i)) return;
                minPositionWeight[i] = Mathf.Min(minPositionWeight[i], positionWeight[i]);
                maxPositionWeight[i] = Mathf.Max(maxPositionWeight[i], positionWeight[i]);
                minRotationWeight[i] = Mathf.Min(minRotationWeight[i], rotationWeight[i]);
                maxRotationWeight[i] = Mathf.Max(maxRotationWeight[i], rotationWeight[i]);
                nonzeroPositionWeight[i] |= positionWeight[i] > 0f;
                nonzeroRotationWeight[i] |= rotationWeight[i] > 0f;
                goalSamples[i]++;
            }
            callbackElapsed = Time.realtimeSinceStartup - session.ArmTime;
            if (!Need(Finite(callbackElapsed) && callbackElapsed >= 0f, "CALLBACK_TIME")) return;
            callbackFrame = Time.frameCount; pending = true;
        }

        private void LateUpdate()
        {
            if (!armed || complete || session == null || session.Failure != null) return;
            if (Time.frameCount == armedFrame && !pending) return;
            if (!Need(pending && callbackFrame == Time.frameCount && lateFrame != Time.frameCount, "SAME_FRAME_LATE")) return;
            if (!CheckLeaseAndPose()) return;
            lateFrame = Time.frameCount; completedLate++; pending = false;

            if (!session.Started)
            {
                if (phase <= initialPhase) return;
                Array.Copy(pose, candidatePose, pose.Length);
                candidateRoot = root; candidateHeading = heading; candidateFrame = Time.frameCount;
                session.TryCommitBaseline(Time.frameCount);
                return;
            }
            if (Time.frameCount == session.BaselineFrame) return;

            float elapsed = Time.realtimeSinceStartup - session.StartedAt;
            if (!Need(Finite(elapsed) && elapsed >= 0f, "WINDOW_TIME")) return;
            float footDrift = 0f;
            string acceptanceFailure = null;
            for (int i = 0; i < pose.Length; i++)
            {
                landmarkDrift[i] = Vector3.Distance(baselinePose[i], pose[i]);
                if (!Need(Finite(landmarkDrift[i]), "LANDMARK_NONFINITE_" + i)) return;
                footDrift = Mathf.Max(footDrift, landmarkDrift[i]);
                maxLandmarkDrift[i] = Mathf.Max(maxLandmarkDrift[i], landmarkDrift[i]);
                if (acceptanceFailure == null && landmarkDrift[i] > .001f) acceptanceFailure = "LANDMARK_DRIFT_" + i;
            }
            float rootDrift = Vector3.Distance(baselineRoot, root);
            float headingDrift = Quaternion.Angle(baselineHeading, heading);
            if (!Need(Finite(rootDrift) && Finite(headingDrift), "ROOT_NONFINITE")) return;
            if (acceptanceFailure == null && rootDrift > .001f) acceptanceFailure = "ROOT_DRIFT";
            if (acceptanceFailure == null && headingDrift > .05f) acceptanceFailure = "HEADING_DRIFT";
            maxFootDrift = Mathf.Max(maxFootDrift, footDrift);
            maxRootDrift = Mathf.Max(maxRootDrift, rootDrift);
            maxHeadingDrift = Mathf.Max(maxHeadingDrift, headingDrift);
            samples++;
            if (acceptanceFailure != null)
            {
                session.FailSample(this, acceptanceFailure, elapsed, footDrift, rootDrift, headingDrift);
                return;
            }
            if (elapsed >= lastRow + RowInterval || elapsed >= Window)
            {
                lastRow = elapsed;
                session.Row(this, elapsed, footDrift, rootDrift, headingDrift);
            }
            if (elapsed >= Window)
            {
                complete = true; endpointFrame = Time.frameCount; endpointElapsed = elapsed; session.MarkComplete(index);
            }
        }

        private bool CheckLeaseAndPose()
        {
            if (!Need((bool)ready.holds.Invoke(body, new[] { identity }), "IDENTITY_CHANGED")) return false;
            currentHosts.Clear(); animator.GetComponents(currentHosts);
            if (!Need(currentHosts.Count == hostBehaviours.Length, "HOST_COUNT_CHANGED")) return false;
            for (int i = 0; i < hostBehaviours.Length; i++)
                if (!Need(currentHosts[i] == hostBehaviours[i] && currentHosts[i] != null
                    && currentHosts[i].enabled == hostEnabled[i] && currentHosts[i].isActiveAndEnabled == hostActive[i], "HOST_LEASE_CHANGED")) return false;
            if (!Need(body.isActiveAndEnabled && !body.ReducedMotion && body.IsStill
                && !body.IsSeated && !body.IsTalking && !body.IsArguing && !body.IsReacting
                && body.CharacterId == ready.characterId && body.Frame == bodyFrame
                && body.LastReaction == reaction && body.LastGesture == gesture && body.Activity == activity,
                "PRESENTATION_CHANGED")) return false;
            if (!Need(Time.timeScale == timeScale && animator.cullingMode == culling && animator.runtimeAnimatorController == ready.controller
                && animator.isActiveAndEnabled && animator.isInitialized && animator.isHuman && animator.avatar != null && animator.avatar.isValid
                && !animator.applyRootMotion && animator.speed == 1f && animator.updateMode == AnimatorUpdateMode.Normal, "ANIMATOR_CHANGED")) return false;
            if (!Need(!animator.IsInTransition(0), "IDLE_TRANSITION")) return false;
            var state = animator.GetCurrentAnimatorStateInfo(0); clips.Clear(); animator.GetCurrentAnimatorClipInfo(0, clips);
            if (!Need(state.IsName("Idle") && state.fullPathHash == stateHash && clips.Count == 1 && clips[0].clip == ready.idle
                && Weight(clips[0].weight) && clips[0].weight > 0f && Finite(state.normalizedTime) && state.normalizedTime >= lastPhase,
                "IDLE_SELECTION")) return false;
            phase = state.normalizedTime; lastPhase = phase;
            root = body.transform.position; heading = body.transform.rotation;
            if (!Need(Finite(root) && ValidRotation(heading), "ROOT_POSE")) return false;
            for (int i = 0; i < bones.Length; i++)
            {
                if (!Need(animator.GetBoneTransform(BoneKinds[i]) == bones[i], "BONE_MAPPING_CHANGED_" + i)) return false;
                pose[i] = bones[i].position;
                if (!Need(Finite(pose[i]), "LANDMARK_POSE")) return false;
            }
            return true;
        }

        private void CommitBaseline(int frame)
        {
            if (!Need(!baseline && candidateFrame == frame, "BASELINE_RECAPTURE")) return;
            Array.Copy(candidatePose, baselinePose, pose.Length);
            baselineRoot = candidateRoot; baselineHeading = candidateHeading; baselinePhase = phase; baseline = true;
        }

        private bool Need(bool condition, string code)
        {
            if (!condition) session?.Fail(index, code);
            return condition;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
        private static bool ValidRotation(Quaternion value) => Finite(value)
            && value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w > .000001f;
        private static bool Weight(float value) => Finite(value) && value >= 0f && value <= 1f;
        private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string Bool(bool value) => value ? "true" : "false";
        private static void Vector(StringBuilder text, Vector3 value) => text.Append(Number(value.x)).Append(',').Append(Number(value.y)).Append(',').Append(Number(value.z));
        private static void Rotation(StringBuilder text, Quaternion value) => text.Append(Number(value.x)).Append(',').Append(Number(value.y)).Append(',').Append(Number(value.z)).Append(',').Append(Number(value.w));

        private sealed class Session
        {
            private readonly Report report;
            private readonly Source21ProvidedIdleFootIkObserver[] readers = new Source21ProvidedIdleFootIkObserver[2];
            private readonly bool[] completed = new bool[2];
            private string failureSample;
            internal string Failure { get; private set; }
            internal float ArmTime, StartedAt;
            internal int BaselineFrame { get; private set; } = -1;
            internal bool Started => BaselineFrame >= 0;
            internal bool Done => completed[0] && completed[1];

            internal Session(string provenance)
            {
                report = new Report();
                report.Append("SOURCE21-IDLE-FOOT-IK version=1 fixture=provided provenance=" + provenance
                    + " durationRequested=2 culling=AlwaysAnimate farPark=true changedField=Idle.iKOnFeet"
                    + " fixtureSetterCount=0 fixtureOwnedMask=0 nativeWeightMustBeZero=false"
                    + " foreignWriterOwnership=unproven wholeFrameFinal=false shippingAcceptance=false\n", false);
            }

            internal void Register(int index, Source21ProvidedIdleFootIkObserver reader) { readers[index] = reader; }
            internal void Fail(int body, string code) { if (Failure == null) Failure = "body=" + body + " code=" + code; }
            internal void Line(string value, bool row)
            { if (!report.Append(value, row)) Fail(-1, row ? "REPORT_ROW_OR_BYTE_CAP" : "REPORT_BYTE_CAP"); }

            internal void TryCommitBaseline(int frame)
            {
                if (Started || readers[0] == null || readers[1] == null
                    || readers[0].candidateFrame != frame || readers[1].candidateFrame != frame) return;
                readers[0].CommitBaseline(frame); readers[1].CommitBaseline(frame);
                if (Failure != null) return;
                BaselineFrame = frame; StartedAt = Time.realtimeSinceStartup;
                var baseline = new StringBuilder(1536);
                baseline.Append("BASELINE frame=").Append(frame)
                    .Append(" pairedSameLate=true naturallyAdvanced=true recapture=false");
                for (int body = 0; body < readers.Length; body++)
                {
                    var reader = readers[body];
                    baseline.Append(" body").Append(body).Append("Phase=").Append(Number(reader.baselinePhase))
                        .Append(" root="); Vector(baseline, reader.baselineRoot);
                    baseline.Append(" heading="); Rotation(baseline, reader.baselineHeading);
                    for (int landmark = 0; landmark < reader.baselinePose.Length; landmark++)
                    { baseline.Append(" landmark").Append(landmark).Append('='); Vector(baseline, reader.baselinePose[landmark]); }
                }
                baseline.Append('\n'); Line(baseline.ToString(), false);
            }

            internal void MarkComplete(int index) { completed[index] = true; }

            internal void FailSample(Source21ProvidedIdleFootIkObserver reader, string code,
                float elapsed, float feet, float root, float heading)
            {
                if (Failure != null) return;
                var row = new StringBuilder(2048);
                row.Append("FAIL_SAMPLE code=").Append(code).Append(" body=").Append(reader.index)
                    .Append(" callbackFrame=").Append(reader.callbackFrame).Append(" lateFrame=").Append(reader.lateFrame)
                    .Append(" elapsed=").Append(Number(elapsed)).Append(" callbackElapsed=").Append(Number(reader.callbackElapsed))
                    .Append(" phase=").Append(Number(reader.phase)).Append(" maxLandmarkDrift=").Append(Number(feet))
                    .Append(" rootDrift=").Append(Number(root)).Append(" headingDrift=").Append(Number(heading))
                    .Append(" fixtureSetterCount=0 fixtureOwnedMask=0");
                for (int i = 0; i < 2; i++)
                {
                    row.Append(" goal").Append(i).Append('='); Vector(row, reader.goalPosition[i]); row.Append(','); Rotation(row, reader.goalRotation[i]);
                    row.Append(",positionWeight=").Append(Number(reader.positionWeight[i]))
                        .Append(",rotationWeight=").Append(Number(reader.rotationWeight[i]));
                }
                for (int i = 0; i < 4; i++)
                {
                    row.Append(" landmark").Append(i).Append('='); Vector(row, reader.pose[i]);
                    row.Append(" drift=").Append(Number(reader.landmarkDrift[i]));
                }
                row.Append('\n');
                failureSample = Encoding.UTF8.GetByteCount(row.ToString()) <= 2048
                    ? row.ToString() : "FAIL_SAMPLE code=" + code + " body=" + reader.index + " detail=FAIL_SAMPLE_CAP\n";
                Fail(reader.index, code);
            }

            internal void Row(Source21ProvidedIdleFootIkObserver reader, float elapsed, float feet, float root, float heading)
            {
                var row = new StringBuilder(1536);
                row.Append("ROW body=").Append(reader.index).Append(" callbackFrame=").Append(reader.callbackFrame)
                    .Append(" lateFrame=").Append(reader.lateFrame).Append(" elapsed=").Append(Number(elapsed))
                    .Append(" callbackElapsed=").Append(Number(reader.callbackElapsed))
                    .Append(" phase=").Append(Number(reader.phase)).Append(" maxLandmarkDrift=").Append(Number(feet))
                    .Append(" rootDrift=").Append(Number(root)).Append(" headingDrift=").Append(Number(heading))
                    .Append(" fixtureSetterCount=0 fixtureOwnedMask=0");
                for (int i = 0; i < 2; i++)
                {
                    row.Append(" goal").Append(i).Append('='); Vector(row, reader.goalPosition[i]); row.Append(','); Rotation(row, reader.goalRotation[i]);
                    row.Append(",positionWeight=").Append(Number(reader.positionWeight[i]))
                        .Append(",rotationWeight=").Append(Number(reader.rotationWeight[i]));
                }
                for (int i = 0; i < 4; i++)
                {
                    row.Append(" landmark").Append(i).Append('='); Vector(row, reader.pose[i]);
                    row.Append(" drift=").Append(Number(reader.landmarkDrift[i]));
                }
                row.Append('\n'); Line(row.ToString(), true);
            }

            internal string Finish(Source21ProvidedIdleFootIkObserver[] values)
            {
                var tail = new StringBuilder(4096);
                if (failureSample != null) tail.Append(failureSample);
                tail.Append("SUMMARY passed=").Append(Failure == null && Done ? "true" : "false")
                    .Append(" failure=").Append(Failure ?? "none").Append(" baselineFrame=").Append(BaselineFrame)
                    .Append(" fixtureSetterCount=0 fixtureOwnedMask=0 nativeWeightMustBeZero=false\n");
                for (int b = 0; b < values.Length; b++)
                {
                    var r = values[b];
                    if (r == null) { tail.Append("BODY ").Append(b).Append(" absent=true\n"); continue; }
                    tail.Append("BODY ").Append(b).Append(" callbacks=").Append(r.callbacks).Append(" completedLate=").Append(r.completedLate)
                        .Append(" samples=").Append(r.samples).Append(" endpointFrame=").Append(r.endpointFrame)
                        .Append(" endpointElapsed=").Append(Number(r.endpointElapsed))
                        .Append(" initialPhase=").Append(Number(r.initialPhase)).Append(" baselinePhase=").Append(Number(r.baselinePhase))
                        .Append(" endpointPhase=").Append(Number(r.phase))
                        .Append(" maxFootDrift=").Append(Number(r.maxFootDrift)).Append(" maxRootDrift=").Append(Number(r.maxRootDrift))
                        .Append(" maxHeadingDrift=").Append(Number(r.maxHeadingDrift));
                    for (int i = 0; i < 4; i++)
                        tail.Append(" landmark").Append(i).Append("MaxDrift=").Append(Number(r.maxLandmarkDrift[i]));
                    for (int i = 0; i < 2; i++)
                    {
                        tail.Append(" goal").Append(i);
                        if (r.goalSamples[i] == 0) tail.Append("WeightsUnobserved=true samples=0");
                        else tail.Append("PositionWeightMin=").Append(Number(r.minPositionWeight[i]))
                            .Append(" max=").Append(Number(r.maxPositionWeight[i])).Append(" nonzero=").Append(Bool(r.nonzeroPositionWeight[i]))
                            .Append(" rotationWeightMin=").Append(Number(r.minRotationWeight[i]))
                            .Append(" max=").Append(Number(r.maxRotationWeight[i])).Append(" nonzero=").Append(Bool(r.nonzeroRotationWeight[i]))
                            .Append(" samples=").Append(r.goalSamples[i]);
                    }
                    tail.Append('\n');
                }
                report.AppendTail(tail.ToString());
                return report.Text;
            }
        }

        private sealed class Report
        {
            private const int MaxBytes = 4 * 1024 * 1024, MaxRows = 1024, Reserve = 4096;
            private readonly StringBuilder text = new StringBuilder(8192);
            private int bytes, rows;
            internal string Text => text.ToString();

            internal bool Append(string value, bool row)
            {
                int size = Encoding.UTF8.GetByteCount(value);
                if ((row && rows >= MaxRows) || size > MaxBytes - Reserve - bytes) return false;
                text.Append(value); bytes += size; if (row) rows++;
                return true;
            }

            internal void AppendTail(string value)
            {
                int size = Encoding.UTF8.GetByteCount(value);
                if (size > Reserve || bytes + size > MaxBytes)
                    value = "SUMMARY passed=false failure=REPORT_TAIL_CAP fixtureSetterCount=0 fixtureOwnedMask=0\n";
                text.Append(value);
            }
        }
    }
}
#endif
