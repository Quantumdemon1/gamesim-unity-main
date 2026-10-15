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
    /// <summary>Actual imported mocap playback. No ceremony, rendered-body, contact or UMA acceptance claim.
    /// The IK feasibility and Idle diagnostic probes live in Gamesim.DiagnosticsPlayModeTests (GAMESIM_DIAGNOSTICS).</summary>
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
                // Explicit == null: GetComponent returns Unity's fake null in the editor, which ?? never replaces.
                var animator = root.GetComponent<Animator>();
                if (animator == null) animator = root.AddComponent<Animator>();
                animator.avatar = avatar; animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.updateMode = AnimatorUpdateMode.Normal;
                body = new CharacterBody(root, animator, false, this);
                return true;
            }
            public bool TryCreate(string id, Transform parent, Color colour, out CharacterBody body) { body = default; return false; }
            public void SetWardrobeColor(in CharacterBody body, Color colour) { }
        }
    }
}
#endif
