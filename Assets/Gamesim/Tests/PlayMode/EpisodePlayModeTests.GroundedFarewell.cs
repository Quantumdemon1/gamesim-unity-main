using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private string FarewellEndpoint(string firstId, string secondId)
        {
            string Side(string id)
            {
                var body = Presentation(HouseguestBody(id));
                var motion = body.GetComponent<HouseNpcMotion>();
                var seat = body.GetComponent<HouseSeatPresentation>();
                bool read = body.TryReadFarewell(out var pose);
                return id + " primitive=" + body.IsPrimitiveBody + ", take=" + body.HasFarewellTake
                    + ", read=" + read + ", idle=" + pose.Idle + ", inTake=" + pose.InTake + ", progress=" + pose.NormalizedTime
                    + ", seatActive=" + (seat != null && seat.Active) + ", parked=" + motion.IsParked
                    + ", lease=" + motion.LeaseId + ", arrival=" + motion.ArrivalFailure;
            }
            return "Actual endpoint frame=" + Time.frameCount + ", delta=" + Time.deltaTime
                + ", reason=" + director.GoodbyeFarewellFailure + "\n" + director.GoodbyeFarewellDiagnostic + "\n" + Side(firstId) + "\n" + Side(secondId);
        }

        private IEnumerator WaitForFarewellEndpoint(System.Func<bool> predicate, float seconds, string message, string firstId, string secondId)
        {
            float by = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < by) yield return null;
            bool reached = predicate();
            Assert.That(reached, Is.True, message + (reached ? "" : "\n" + FarewellEndpoint(firstId, secondId)));
        }

        // This observes the real ceremony transaction; it neither builds an owner nor drives its Animator.
        private IEnumerator ObserveFarewellToIdle(FarewellPair pair, float absoluteGoodbyeEnd, JObject savedGraph)
        {
            int samples = 0;
            while (pair != null && pair.Stage == FarewellPair.Phase.Playing && Time.unscaledTime < absoluteGoodbyeEnd)
            {
                Assert.That(JToken.DeepEquals(savedGraph, UvPayload(director.Snapshot)), Is.True, "Presentation preserves every saved field.");
                if (pair.First.TryReadFarewell(out var a) && pair.Second.TryReadFarewell(out var b) && a.InTake && b.InTake)
                {
                    foreach (var point in new[] { a.Head, b.Head, a.RightHand, b.RightHand, a.LeftFoot, a.RightFoot, b.LeftFoot, b.RightFoot })
                    {
                        var projected = cameraRig.ViewCamera.WorldToViewportPoint(point);
                        Assert.That(projected.z, Is.GreaterThan(cameraRig.ViewCamera.nearClipPlane), "Actual animated landmark ahead of actual camera.");
                        Assert.That(projected.x, Is.InRange(.05f, .95f));
                        Assert.That(projected.y, Is.InRange(.05f, .95f));
                    }
                    samples++;
                }
                yield return null;
            }
            Assert.That(pair != null && pair.Stage == FarewellPair.Phase.Complete, Is.True,
                "The actual take finishes inside the unchanged whole goodbye: " + (pair != null ? pair.ReleaseReason : "holder lost")
                    + (pair != null && pair.Stage != FarewellPair.Phase.Complete ? "\n" + pair.FailureDiagnostic : ""));
            Assert.That(samples, Is.GreaterThan(0), "Actual during-take projection, not a pre-trigger predicted hand.");
            Assert.That(director.GoodbyeFarewellStandingObservations, Is.GreaterThanOrEqualTo(2), "Two actual local post-unpark readiness observations.");
            Assert.That(pair.FiredFrame, Is.GreaterThan(director.GoodbyeFarewellCameraCutFrame), "A later actual camera frame, not same-Tick Cut readiness.");
            Assert.That(pair.FirstEnteredFrame, Is.GreaterThanOrEqualTo(pair.FiredFrame));
            Assert.That(pair.SecondEnteredFrame, Is.EqualTo(pair.FirstEnteredFrame));
            Assert.That(pair.FirstNeutral && pair.SecondNeutral, Is.True);
            Assert.That(pair.LiftedPalmSamples, Is.GreaterThan(0));
            Assert.That(pair.MaximumFootDrift, Is.LessThanOrEqualTo(FarewellPair.FootTolerance));
            Assert.That(pair.MaximumRootDrift, Is.LessThanOrEqualTo(FarewellPair.RootTolerance));
            Assert.That(pair.MaximumHeadingDrift, Is.LessThanOrEqualTo(FarewellPair.HeadingTolerance));
            // The director restores the face on its next Update AFTER the pair's actual neutral/Idle readback.
            yield return null;
        }

        [UnityTest]
        public IEnumerator GroundedFarewell_SkippingReleasesBothOriginalPrincipalsBeforeDeparture()
        {
            yield return BeginFarewellFixture();
            var pair = director.GoodbyeFarewell;
            var saved = UvPayload(director.Snapshot);
            director.SkipWalkOut(); // Existing public action consumes the current Goodbye through SkipTheGoodbye.
            yield return null;
            Assert.That(pair == null || pair.Released, Is.True);
            Assert.That(director.GoodbyeFarewell, Is.Null);
            Assert.That(JToken.DeepEquals(saved, UvPayload(director.Snapshot)), Is.True);
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null, "No leaked ceremony lease enters the departure owner.");
        }

        [UnityTest]
        public IEnumerator GroundedFarewell_ReducedMotionSeparatesProvidedCancellationFromPrimitiveDiagnostics()
        {
            yield return BeginFarewellFixture();
            var pair = director.GoodbyeFarewell;
            if (pair == null)
            {
                // This is the explicitly primitive clone contract, not provided-body cancellation acceptance.
                var diagnostics = SceneComponents<CharacterPresentation>().Where(p => p.LastReaction == CharacterPresentation.Reaction.Farewell).ToArray();
                Assert.That(diagnostics.Length, Is.EqualTo(2));
                Assert.That(diagnostics.All(p => p.IsPrimitiveBody), Is.True);
                Assert.That(director.GoodbyeFarewellFailure, Is.Null);
                yield break;
            }
            pair.First.SetReducedMotion(true);
            yield return null;
            yield return null;
            Assert.That(pair.Stage, Is.EqualTo(FarewellPair.Phase.Cancelled), pair.ReleaseReason);
            float by = Time.realtimeSinceStartup + .5f;
            while ((pair.First.IsReacting || pair.Second.IsReacting) && Time.realtimeSinceStartup < by) yield return null;
            Assert.That(pair.First.IsReacting || pair.Second.IsReacting, Is.False, "Both actual takes are cancelled, not one-sided playback.");
            director.SkipWalkOut();
            yield return null;
            Assert.That(director.GoodbyeFarewell, Is.Null);
        }

        [UnityTest]
        public IEnumerator GroundedFarewell_TheOriginalOwnerCountsNewActualIdleFramesBeforeItsCameraCut()
        {
            yield return InstallStagedSeason(52, AtEviction);
            yield return PlayUntilTheCeremony();
            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing,
                EpisodeDirector.SummonsHardSeconds(CeremonyPace.Suspenseful) + 6f, "the real staged eviction reaches its card");
            string leaving = director.DepartingId;
            string staying = director.Snapshot.nominees.Single(id => id != leaving);
            var saved = UvPayload(director.Snapshot);
            var vote = SceneComponents<VoteReveal>().Single();
            vote.SkipToResult(); vote.Cancel();
            yield return null;
            var first = Presentation(HouseguestBody(leaving)); var second = Presentation(HouseguestBody(staying));
            bool primitive = first.IsPrimitiveBody && second.IsPrimitiveBody;
            float by = Time.realtimeSinceStartup + 2f; // Observation only; the product's existing absolute1.5 cap is unchanged.
            while ((primitive ? first.LastReaction != CharacterPresentation.Reaction.Farewell
                    || second.LastReaction != CharacterPresentation.Reaction.Farewell : director.GoodbyeFarewellCameraCutFrame < 0)
                && Time.realtimeSinceStartup < by)
            {
                Assert.That(JToken.DeepEquals(saved, UvPayload(director.Snapshot)), Is.True);
                Assert.That(director.GoodbyeFarewellIdlePreparationAttempts, Is.InRange(0, 1));
                Assert.That(director.GoodbyeFarewellIdlePreparationCalls, Is.InRange(0, 2));
                yield return null;
            }
            Assert.That(JToken.DeepEquals(saved, UvPayload(director.Snapshot)), Is.True, "Every save field stays unchanged.");
            bool noFailure = director.GoodbyeFarewellFailure == null;
            Assert.That(noFailure, Is.True, noFailure ? null : FarewellEndpoint(leaving, staying));
            if (primitive)
            {
                Assert.That(first.LastReaction, Is.EqualTo(CharacterPresentation.Reaction.Farewell));
                Assert.That(second.LastReaction, Is.EqualTo(CharacterPresentation.Reaction.Farewell));
                Assert.That(director.GoodbyeFarewellIdleObservations, Is.Zero);
                Assert.That(director.GoodbyeFarewellIdlePreparationAttempts, Is.Zero);
                Assert.That(director.GoodbyeFarewellIdlePreparationCalls, Is.Zero);
                Debug.Log("[Gamesim] Farewell readiness: primitive diagnostic ONLY; no actual Idle/WalkStop witness.");
                yield break; // Explicit primitive diagnostic, NEVER an actual Idle/WalkStop acceptance claim.
            }
            Assert.That(first.IsPrimitiveBody || second.IsPrimitiveBody, Is.False);
            Assert.That(director.GoodbyeFarewellIdlePreparationFrame, Is.GreaterThanOrEqualTo(0));
            Assert.That(director.GoodbyeFarewellStandingObservations, Is.GreaterThanOrEqualTo(2));
            Assert.That(director.GoodbyeFarewellIdleObservations, Is.GreaterThanOrEqualTo(2),
                "Separate NEW evaluated actual-Idle observations, not the older physical-ready count.");
            Assert.That(director.GoodbyeFarewellIdleObservedFrame,
                Is.GreaterThan(director.GoodbyeFarewellIdlePreparationFrame + 1));
            Assert.That(director.GoodbyeFarewellCameraCutFrame, Is.GreaterThanOrEqualTo(director.GoodbyeFarewellIdleObservedFrame));
            Assert.That(director.GoodbyeFarewellIdlePreparationAttempts,
                Is.EqualTo(director.GoodbyeFarewellIdlePreparationCalls > 0 ? 1 : 0));
            Assert.That(first.HasFarewellTake && second.HasFarewellTake, Is.True);
            Assert.That(director.GoodbyeFarewellIdleObservedFrame, Is.LessThanOrEqualTo(Time.frameCount));
            Debug.Log("[Gamesim] Farewell readiness: actual provided principals=" + leaving + "/" + staying
                + " physicalFrames=" + director.GoodbyeFarewellStandingObservations
                + " newActualIdleFrames=" + director.GoodbyeFarewellIdleObservations
                + " preparationFrame=" + director.GoodbyeFarewellIdlePreparationFrame
                + " observedIdleFrame=" + director.GoodbyeFarewellIdleObservedFrame
                + " cutFrame=" + director.GoodbyeFarewellCameraCutFrame
                + " attempts=" + director.GoodbyeFarewellIdlePreparationAttempts
                + " completedAnimatorCalls=" + director.GoodbyeFarewellIdlePreparationCalls);
            // These are captured actual-Idle frame facts. Do not re-label a later take/crossing as current Idle.
            director.SkipWalkOut();
            yield return null;
            Assert.That(director.GoodbyeFarewell, Is.Null);
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
            Assert.That(JToken.DeepEquals(saved, UvPayload(director.Snapshot)), Is.True);
        }


#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
        // These require actual provided bodies. A primitive clone or a missing ordinary branch
        // FAILS as a missing witness; it is never promoted from the generic diagnostic case.
        private sealed class FarewellReadinessRoute
        {
            internal object Stage;
            internal CharacterPresentation First, Second;
            internal HouseMeetingCoordinator Owner;
            internal HouseInteractionAnchor FirstPlace, SecondPlace;
            internal string FirstToken, SecondToken;
            internal JObject Saved;
            internal float Deadline, GoodbyeEnd;
        }
        private FarewellReadinessRoute farewellReadinessRoute;

#if !GAMESIM_UMA
        // This lease supplies only the existing imported skeleton. It is not a wardrobe provider.
        private ProvidedFarewellFixtureProvider providedFarewellFixtureProvider;
        private bool providedFarewellFixtureReleaseSucceeded;
        private readonly System.Collections.Generic.List<System.Exception> providedFarewellFixtureReleaseFailures
            = new System.Collections.Generic.List<System.Exception>();

        private static void RetainProvidedFarewellReleaseFailure(
            System.Collections.Generic.List<System.Exception> failures, string phase, System.Exception failure)
        {
            failures.Add(new System.InvalidOperationException("Farewell fixture release: " + phase, failure));
            try
            {
                TestContext.Error.WriteLine("[Gamesim] Farewell fixture release FAILED phase=" + phase
                    + " type=" + failure.GetType().FullName);
            }
            catch (System.Exception evidenceFailure)
            {
                // Keep the reporting failure too; do not replace an active original test exception.
                failures.Add(new System.InvalidOperationException("Farewell fixture release evidence", evidenceFailure));
            }
        }

        private sealed class ProvidedFarewellFixtureProvider : IModularCharacterBodyProvider
        {
            private const string Skeleton = "Assets/Gamesim/Art/Authored/Animation/Humanoid/bb_anim_PoseAtEase.fbx";
            private readonly GameObject model;
            private readonly Avatar avatar;
            private readonly RuntimeAnimatorController controller;
            private GameObject pendingClone;
            private readonly System.Collections.Generic.List<GameObject> ownedClones
                = new System.Collections.Generic.List<GameObject>();
            private readonly System.Collections.Generic.List<System.Exception> releaseFailures;

            internal ProvidedFarewellFixtureProvider(System.Collections.Generic.List<System.Exception> failures)
            {
                releaseFailures = failures;
                model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(Skeleton);
                avatar = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(Skeleton).OfType<Avatar>().FirstOrDefault();
                controller = Resources.Load<RuntimeAnimatorController>("Animation/GamesimHumanoid");
                Assert.That(model != null && avatar != null && avatar.isValid && avatar.isHuman && controller != null,
                    Is.True, "MISSING genuine imported Skeleton/valid human Avatar/canonical controller; no fallback.");
            }

            public ICharacterAppearanceCatalog Catalog => null;
            internal int LiveOwnedCloneCount => ownedClones.Count(root => root != null) + (pendingClone != null ? 1 : 0);

            public bool TryCreate(in CharacterBodyRequest request, Transform parent, Color badge, out CharacterBody body)
            {
                body = default;
                GameObject root = null;
                try
                {
                    root = Object.Instantiate(model, parent, false);
                    pendingClone = root; // Retain ownership even if adding the clone to the list fails.
                    ownedClones.Add(root); // Own the actual clone before any Animator configuration can fail.
                    pendingClone = null;
                    var animator = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
                    animator.avatar = avatar; animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.updateMode = AnimatorUpdateMode.Normal;
                    body = new CharacterBody(root, animator, false, this);
                    return true;
                }
                catch
                {
                    try
                    {
                        CharacterBodySource.Unregister(this); // Refuse further builds before releasing any owned clone.
                    }
                    catch (System.Exception failure)
                    {
                        RetainProvidedFarewellReleaseFailure(releaseFailures, "create-unregister", failure);
                    }
                    ReleaseOwnedClones(); // A failed build unwinds ALL owned clones, not just the failed root.
                    throw; // The original builder failure, not a replacement cleanup exception.
                }
            }

            public bool TryCreate(string id, Transform parent, Color colour, out CharacterBody body)
            { body = default; return false; }
            public void SetWardrobeColor(in CharacterBody body, Color colour) { }

            private void ReleaseOwnedClone(GameObject root, string phase)
            {
                try
                {
                    if (root != null) Object.DestroyImmediate(root); // Owned instance only; never the borrowed asset or parent.
                    Assert.That(root == null, Is.True, "Owned farewell fixture clone remains live after release.");
                    ownedClones.Remove(root);
                    if (object.ReferenceEquals(pendingClone, root)) pendingClone = null;
                }
                catch (System.Exception failure)
                {
                    RetainProvidedFarewellReleaseFailure(releaseFailures, phase, failure);
                }
            }

            internal void ReleaseOwnedClones()
            {
                // Do not let one release exception skip any later owned instance.
                if (pendingClone != null) ReleaseOwnedClone(pendingClone, "pending-clone");
                else pendingClone = null;
                for (int i = ownedClones.Count - 1; i >= 0; i--)
                    ReleaseOwnedClone(ownedClones[i], "teardown-clone");
            }
        }
#endif

        private void AcquireProvidedFarewellFixture()
        {
#if !GAMESIM_UMA
            if (providedFarewellFixtureProvider != null)
            {
                try
                {
                    Assert.That(providedFarewellFixtureReleaseFailures, Is.Empty, "A failed lease cannot be reused.");
                    Assert.That(object.ReferenceEquals(CharacterBodySource.Provider, providedFarewellFixtureProvider),
                        Is.True, "Unrelated provider displaced the current readiness lease; do not reorder the registry.");
                }
                catch
                {
                    ReleaseProvidedFarewellFixture(false);
                    throw;
                }
                return; // Existing intra-test reloads use this exact one lease, not stacked registrations.
            }

            Assert.That(providedFarewellFixtureReleaseFailures, Is.Empty, "An earlier fixture release failure is retained.");
            var previous = CharacterBodySource.Provider;
            Assert.That(previous, Is.Null, "Unexpected preexisting NoUMA provider; fixture refuses to replace it.");
            var owned = new ProvidedFarewellFixtureProvider(providedFarewellFixtureReleaseFailures);
            providedFarewellFixtureProvider = owned; // Ownership precedes Register and its postcondition.
            providedFarewellFixtureReleaseSucceeded = false;
            try
            {
                CharacterBodySource.Register(owned);
                Assert.That(object.ReferenceEquals(CharacterBodySource.Provider, owned), Is.True,
                    "The exact owned provider must be current before staged reload.");
            }
            catch
            {
                ReleaseProvidedFarewellFixture(false);
                throw;
            }
#endif
        }

        private static object ReadFarewellField(object owner, string name)
        {
            Assert.That(owner, Is.Not.Null, "Missing actual captured owner for " + name);
            var field = owner.GetType().GetField(name, System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            Assert.That(field, Is.Not.Null, "Missing captured read-only field " + name);
            return field.GetValue(owner); // READ ONLY: no private field assignment or manual Tick.
        }
        private static T ReadFarewellField<T>(object owner, string name) => (T)ReadFarewellField(owner, name);
        private static Animator ReadFarewellAnimator(CharacterPresentation body)
        {
            Assert.That(body != null && !body.IsPrimitiveBody && body.VisualRoot != null, Is.True,
                "MISSING PROVIDED-BODY WITNESS; primitive diagnostics are not readiness coverage.");
            var animator = body.VisualRoot.GetComponentInChildren<Animator>(true);
            Assert.That(animator != null && animator.isInitialized && animator.isHuman
                && animator.avatar != null && animator.avatar.isValid && animator.runtimeAnimatorController != null, Is.True);
            return animator;
        }
        private static AnimationClip ReadFarewellIdleClip(Animator animator)
        {
            var handle = Resources.Load<AnimatorOverrideController>("Animation/GamesimHumanoid");
            Assert.That(handle != null && handle.runtimeAnimatorController != null
                && !(handle.runtimeAnimatorController is AnimatorOverrideController), Is.True);
            var basis = handle.runtimeAnimatorController;
            var idle = basis.animationClips.Single(clip => clip != null && clip.name == "Idle_Loop");
            if (animator.runtimeAnimatorController == basis) return idle;
            var replaced = animator.runtimeAnimatorController as AnimatorOverrideController;
            Assert.That(replaced != null && replaced.runtimeAnimatorController == basis && replaced.overridesCount <= 128, Is.True);
            var rows = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
            replaced.GetOverrides(rows);
            var value = rows.Single(row => row.Key == idle).Value;
            return value == null ? idle : value; // READ ONLY resolved exact permitted Idle object.
        }
        private static object[] ReadFarewellBodyIdentity(CharacterPresentation body)
        {
            var provided = ReadFarewellField<CharacterBody>(body, "providedBody");
            var animator = ReadFarewellAnimator(body);
            var build = provided.Root.GetComponent<CharacterBodyBuildState>();
            return new object[] { body.VisualRoot, provided.Root, animator, animator.runtimeAnimatorController,
                animator.avatar, build, build != null ? build.Revision : 0 };
        }
        private void AssertFarewellSave(FarewellReadinessRoute route)
            => Assert.That(JToken.DeepEquals(route.Saved, UvPayload(director.Snapshot)), Is.True,
                "Every saved field/revision/RNG remains unchanged during presentation.");

        private IEnumerator BeginProvidedFarewellReadiness(float validCardReadingSeconds = 0f)
        {
            AcquireProvidedFarewellFixture();
            yield return InstallStagedSeason(52, AtEviction);
            yield return PlayUntilTheCeremony();
            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing,
                EpisodeDirector.SummonsHardSeconds(CeremonyPace.Suspenseful) + 6f, "the real eviction card");
            string leaving = director.DepartingId;
            string staying = director.Snapshot.nominees.Single(id => id != leaving);
            var vote = SceneComponents<VoteReveal>().Single();
            float readUntil = Time.realtimeSinceStartup + validCardReadingSeconds;
            while (Time.realtimeSinceStartup < readUntil)
            {
                Assert.That(vote != null && vote.IsPlaying, Is.True, "Only the still-valid public card may be paced.");
                yield return null;
            }
            vote.SkipToResult(); vote.Cancel();
            yield return null;
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Goodbye));
            var stage = ReadFarewellField(director, "ceremonyStage");
            var route = new FarewellReadinessRoute
            {
                Stage = stage, First = Presentation(HouseguestBody(leaving)), Second = Presentation(HouseguestBody(staying)),
                Owner = ReadFarewellField<HouseMeetingCoordinator>(stage, "farewellOwner"),
                FirstPlace = ReadFarewellField<HouseInteractionAnchor>(stage, "farewellPlaceA"),
                SecondPlace = ReadFarewellField<HouseInteractionAnchor>(stage, "farewellPlaceB"),
                FirstToken = ReadFarewellField<string>(stage, "farewellTokenA"),
                SecondToken = ReadFarewellField<string>(stage, "farewellTokenB"),
                Deadline = ReadFarewellField<float>(stage, "farewellAtGoodbyeBy"),
                GoodbyeEnd = ReadFarewellField<float>(stage, "goodbyeUntil"), Saved = UvPayload(director.Snapshot)
            };
            ReadFarewellAnimator(route.First); ReadFarewellAnimator(route.Second);
            Assert.That(route.Owner, Is.Not.Null, "MISSING actual captured two-principal ceremony owner.");
            Assert.That(route.FirstToken != null && route.SecondToken != null, Is.True);
            Assert.That(route.Owner.CeremonyActorHolds(route.First.CharacterId)
                && route.Owner.CeremonyActorHolds(route.Second.CharacterId), Is.True);
            TestContext.WriteLine("Provided readiness route: " + route.First.CharacterId + "/" + route.Second.CharacterId
                + " provider=" + (CharacterBodySource.Provider?.GetType().FullName ?? "none")
                + " frames=" + route.First.Frame + "/" + route.Second.Frame
                + " firstToken=" + route.FirstToken + " secondToken=" + route.SecondToken);
            farewellReadinessRoute = route;
        }

        private IEnumerator WaitForFarewellAuthorization(FarewellReadinessRoute route)
        {
            float observationBy = Time.realtimeSinceStartup + 2f;
            while (ReadFarewellField<int>(route.Stage, "farewellIdlePreparationFrame") < 0
                && Time.realtimeSinceStartup < observationBy && Time.unscaledTime <= route.Deadline)
            { AssertFarewellSave(route); yield return null; }
            Assert.That(ReadFarewellField<int>(route.Stage, "farewellIdlePreparationFrame"), Is.GreaterThanOrEqualTo(0),
                "MISSING ordinary authorization witness inside the original1.5s deadline: "
                    + director.GoodbyeFarewellFailure + " " + director.GoodbyeFarewellDiagnostic);
        }

        private void AssertCapturedFarewellBranch(FarewellReadinessRoute route, bool firstIdle, bool secondIdle)
        {
            var a = ReadFarewellField(route.Stage, "farewellIdleA");
            var b = ReadFarewellField(route.Stage, "farewellIdleB");
            Assert.That(ReadFarewellField<bool>(a, "WasIdle"), Is.EqualTo(firstIdle),
                "MISSING requested FIRST ordinary state combination; no forced-state fallback.");
            Assert.That(ReadFarewellField<bool>(b, "WasIdle"), Is.EqualTo(secondIdle),
                "MISSING requested SECOND ordinary state combination; no forced-state fallback.");
            foreach (var captured in new[] { a, b })
            {
                var source = ReadFarewellField<AnimationClip>(captured, "SourceMotion");
                var expected = ReadFarewellField<AnimationClip>(captured,
                    ReadFarewellField<bool>(captured, "WasIdle") ? "IdleMotion" : "StopMotion");
                Assert.That(source, Is.SameAs(expected), "Actual captured clip object, not only a state name.");
                Assert.That(source != null && source.humanMotion && source.length > 0f, Is.True);
                Assert.That(ReadFarewellField<int>(captured, "Frame"),
                    Is.EqualTo(ReadFarewellField<int>(route.Stage, "farewellIdlePreparationFrame")));
            }
            int required = (firstIdle ? 0 : 1) + (secondIdle ? 0 : 1);
            Assert.That(ReadFarewellField<bool>(route.Stage, "farewellIdleAttemptUsed"), Is.EqualTo(required > 0));
            Assert.That(ReadFarewellField<int>(route.Stage, "farewellIdleCalls"), Is.EqualTo(required));
        }

        private IEnumerator ObserveFarewellIdleBeforeCut(FarewellReadinessRoute route)
        {
            int authorized = ReadFarewellField<int>(route.Stage, "farewellIdlePreparationFrame");
            int previousFrame = -1, samples = 0; float previousA = 0f, previousB = 0f;
            float by = Time.realtimeSinceStartup + 2f;
            while (ReadFarewellField<int>(route.Stage, "farewellCutFrame") < 0 && Time.realtimeSinceStartup < by)
            {
                AssertFarewellSave(route);
                int frame = ReadFarewellField<int>(route.Stage, "farewellIdleFrame");
                if (frame > previousFrame)
                {
                    float a = ReadFarewellField<float>(route.Stage, "farewellIdlePhaseA");
                    float b = ReadFarewellField<float>(route.Stage, "farewellIdlePhaseB");
                    Assert.That(frame, Is.GreaterThan(authorized), "Authorization cannot count as an evaluation.");
                    if (previousFrame >= 0)
                    {
                        Assert.That(frame, Is.EqualTo(previousFrame + 1));
                        Assert.That(a > previousA && b > previousB, Is.True, "BOTH actual recorded Idle phases advance.");
                    }
                    previousFrame = frame; previousA = a; previousB = b; samples++;
                }
                Assert.That(ReadFarewellField<int>(route.Stage, "farewellIdleCalls"), Is.InRange(0, 2));
                yield return null;
            }
            // The cut tick may also have recorded the second Idle sample before this coroutine resumed.
            int finalFrame = ReadFarewellField<int>(route.Stage, "farewellIdleFrame");
            if (finalFrame > previousFrame)
            {
                float a = ReadFarewellField<float>(route.Stage, "farewellIdlePhaseA");
                float b = ReadFarewellField<float>(route.Stage, "farewellIdlePhaseB");
                Assert.That(previousFrame >= 0 && finalFrame == previousFrame + 1 && a > previousA && b > previousB, Is.True);
                samples++;
            }
            Assert.That(ReadFarewellField<int>(route.Stage, "farewellIdleFrames"), Is.GreaterThanOrEqualTo(2));
            Assert.That(samples, Is.GreaterThanOrEqualTo(2), "Two distinct captured natural evaluations must be witnessed.");
            Assert.That(finalFrame, Is.GreaterThan(authorized + 1));
            Assert.That(ReadFarewellField<int>(route.Stage, "farewellCutFrame"), Is.GreaterThanOrEqualTo(finalFrame),
                "MISSING actual cut after new Idle evidence: " + director.GoodbyeFarewellFailure);
            AssertFarewellSave(route);
        }

        private IEnumerator WaitForFarewellBeforeCommit(FarewellReadinessRoute route)
        {
            float by = Time.realtimeSinceStartup + 2f;
            while (ReadFarewellField<int>(route.Stage, "farewellReadyFrames") < 1
                && Time.realtimeSinceStartup < by && Time.unscaledTime <= route.Deadline) yield return null;
            Assert.That(ReadFarewellField<int>(route.Stage, "farewellReadyFrames"), Is.EqualTo(1),
                "MISSING genuine first post-unpark physical observation before authorization.");
            Assert.That(ReadFarewellField<int>(route.Stage, "farewellIdlePreparationFrame"), Is.EqualTo(-1));
            Assert.That(route.First.IsSeated || route.Second.IsSeated
                || route.First.GetComponent<HouseNpcMotion>().IsParked || route.Second.GetComponent<HouseNpcMotion>().IsParked, Is.False);
        }
        private void AssertNoFarewellPreparation(FarewellReadinessRoute route)
        {
            Assert.That(ReadFarewellField<bool>(route.Stage, "farewellIdleAttemptUsed"), Is.False);
            Assert.That(ReadFarewellField<int>(route.Stage, "farewellIdleCalls"), Is.Zero);
            Assert.That(ReadFarewellField<int>(route.Stage, "farewellCutFrame"), Is.EqualTo(-1));
            Assert.That(director.GoodbyeFarewell, Is.Null);
            Assert.That(route.First.LastReaction == CharacterPresentation.Reaction.Farewell
                || route.Second.LastReaction == CharacterPresentation.Reaction.Farewell, Is.False);
            AssertFarewellSave(route);
        }
        private static AnimationClip ReadFarewellSelectedClip(Animator animator, string clipName)
        {
            var handle = Resources.Load<AnimatorOverrideController>("Animation/GamesimHumanoid");
            Assert.That(handle != null && handle.runtimeAnimatorController != null
                && !(handle.runtimeAnimatorController is AnimatorOverrideController), Is.True);
            var basis = handle.runtimeAnimatorController;
            Assert.That(basis.animationClips.Length, Is.LessThanOrEqualTo(128));
            var motion = basis.animationClips.Single(clip => clip != null && clip.name == clipName);
            Assert.That(motion.humanMotion && motion.length > 0f, Is.True);
            if (animator.runtimeAnimatorController == basis) return motion;
            var replaced = animator.runtimeAnimatorController as AnimatorOverrideController;
            Assert.That(replaced != null && replaced.runtimeAnimatorController == basis && replaced.overridesCount <= 128, Is.True);
            var rows = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
            replaced.GetOverrides(rows);
            var value = rows.Single(row => row.Key == motion).Value;
            Assert.That(value == null || value == motion, Is.True,
                "These foreign/WalkStop motions are not production Idle/Run override slots.");
            return motion; // READ ONLY: exact original clip, not a name-only playback witness.
        }

        private static bool PendingOrCurrentFarewellForeign(CharacterPresentation body, int cue)
        {
            if (cue == 3) return body.Activity == CharacterPresentation.BodyActivity.Cooking;
            var animator = ReadFarewellAnimator(body);
            string trigger = cue == 0 ? "ReactFurious" : cue == 1 ? "ReactShocked" : "GestureShrug";
            string stateName = cue == 2 ? "Shrug" : trigger;
            string clipName = cue == 0 ? "React_furious" : cue == 1 ? "React_shocked" : "React_shrug";
            var motion = ReadFarewellSelectedClip(animator, clipName);
            bool pending = animator.GetBool(Animator.StringToHash(trigger))
                && (cue == 2 || ReadFarewellField<int>(body, "pendingReaction")
                    == (int)(cue == 0 ? CharacterPresentation.Reaction.Furious : CharacterPresentation.Reaction.Shocked));
            // Passive snapshots of the SAME existing native reads. A consumed cue or
            // zero-weight selection is not promoted to positive playback evidence.
            var currentClips = animator.GetCurrentAnimatorClipInfo(0);
            var currentState = animator.GetCurrentAnimatorStateInfo(0);
            bool transition = animator.IsInTransition(0);
            var incomingClips = transition ? animator.GetNextAnimatorClipInfo(0) : System.Array.Empty<AnimatorClipInfo>();
            var incomingState = transition ? animator.GetNextAnimatorStateInfo(0) : default;
            bool Selected(AnimatorStateInfo state, AnimatorClipInfo[] clips)
                => state.IsName(stateName) && clips.Length == 1 && clips[0].clip == motion
                    && clips[0].weight > 0f && clips[0].weight <= 1f
                    && !float.IsNaN(state.normalizedTime) && !float.IsInfinity(state.normalizedTime);
            bool current = Selected(currentState, currentClips);
            bool incoming = transition && Selected(incomingState, incomingClips);
            TestContext.WriteLine("Exact foreign observation frame=" + Time.frameCount + " cue=" + trigger
                + " pending=" + pending + " currentPositive=" + current + " incomingPositive=" + incoming);
            if (!pending && !current && !incoming)
                TestContext.WriteLine("MISSING exact foreign witness at sampled boundary: frame=" + Time.frameCount
                    + " cue=" + trigger + " transition=" + transition
                    + " currentState=" + currentState.fullPathHash + " currentPhase=" + currentState.normalizedTime
                    + " currentCount=" + currentClips.Length
                    + " currentExactMotion=" + (currentClips.Length == 1 && currentClips[0].clip == motion)
                    + " currentWeight=" + (currentClips.Length == 1 ? currentClips[0].weight : -1f)
                    + " incomingState=" + incomingState.fullPathHash + " incomingPhase=" + incomingState.normalizedTime
                    + " incomingCount=" + incomingClips.Length
                    + " incomingExactMotion=" + (incomingClips.Length == 1 && incomingClips[0].clip == motion)
                    + " incomingWeight=" + (incomingClips.Length == 1 ? incomingClips[0].weight : -1f));
            return pending || current || incoming; // Exact predicate UNCHANGED; diagnostics are not an owner fallback.
        }

        private static bool CapturedFarewellWalkStops(FarewellReadinessRoute route)
        {
            int frame = ReadFarewellField<int>(route.Stage, "farewellIdlePreparationFrame");
            var records = new[] { ReadFarewellField(route.Stage, "farewellIdleA"), ReadFarewellField(route.Stage, "farewellIdleB") };
            var bodies = new[] { route.First, route.Second };
            for (int i = 0; i < records.Length; i++)
            {
                var captured = records[i];
                var motion = ReadFarewellField<AnimationClip>(captured, "SourceMotion");
                if (ReadFarewellField<bool>(captured, "WasIdle")
                    || ReadFarewellField<int>(captured, "Frame") != frame || frame != Time.frameCount
                    || motion == null || motion != ReadFarewellField<AnimationClip>(captured, "StopMotion")
                    || !motion.humanMotion || !(motion.length > 0f)) return false;
                var identity = ReadFarewellField(captured, "Body");
                var actual = ReadFarewellBodyIdentity(bodies[i]);
                string[] fields = { "Visual", "ProvidedRoot", "Animator", "Controller", "Avatar", "Build", "RequestRevision" };
                for (int j = 0; j < fields.Length; j++)
                    if (!object.Equals(ReadFarewellField(identity, fields[j]), actual[j])) return false;
            }
            return true; // Actual stored capture, never a fabricated second write or phase.
        }

        private static void AssertActualFarewellWalkStopBeforeControl(CharacterPresentation body)
        {
            var animator = ReadFarewellAnimator(body);
            var state = animator.GetCurrentAnimatorStateInfo(0);
            var clips = animator.GetCurrentAnimatorClipInfo(0);
            Assert.That(body.isActiveAndEnabled && !animator.IsInTransition(0) && state.IsName("WalkStop")
                && clips.Length == 1 && clips[0].clip == ReadFarewellSelectedClip(animator, "WalkStop")
                && clips[0].weight > 0f && clips[0].weight <= 1f, Is.True,
                "MISSING actual positive nontransitioning WalkStop before lifecycle control; no forced-state fallback.");
        }

        private void AssertNoNewFarewellTake(FarewellReadinessRoute route, int calls, bool attempted)
        {
            Assert.That(ReadFarewellField<int>(route.Stage, "farewellIdleCalls"), Is.EqualTo(calls));
            Assert.That(ReadFarewellField<bool>(route.Stage, "farewellIdleAttemptUsed"), Is.EqualTo(attempted));
            Assert.That(ReadFarewellField<int>(route.Stage, "farewellCutFrame"), Is.EqualTo(-1));
            Assert.That(director.GoodbyeFarewell, Is.Null);
            Assert.That(route.First.LastReaction == CharacterPresentation.Reaction.Farewell
                || route.Second.LastReaction == CharacterPresentation.Reaction.Farewell, Is.False);
            Assert.That(ReadFarewellField<float>(route.Stage, "farewellAtGoodbyeBy"), Is.EqualTo(route.Deadline));
            Assert.That(ReadFarewellField<float>(route.Stage, "goodbyeUntil"), Is.EqualTo(route.GoodbyeEnd));
            AssertFarewellSave(route);
        }

        private static void AssertOriginalFarewellLeasesReleased(FarewellReadinessRoute route)
        {
            Assert.That(route.First.GetComponent<HouseNpcMotion>().LeaseId, Is.Not.EqualTo(route.FirstToken));
            Assert.That(route.Second.GetComponent<HouseNpcMotion>().LeaseId, Is.Not.EqualTo(route.SecondToken));
            Assert.That(route.Owner.CeremonyActorHolds(route.First.CharacterId)
                || route.Owner.CeremonyActorHolds(route.Second.CharacterId), Is.False);
            Assert.That(route.Owner.CeremonyPlace(route.First.CharacterId), Is.Null);
            Assert.That(route.Owner.CeremonyPlace(route.Second.CharacterId), Is.Null);
        }

        private IEnumerator ObserveFarewellFirstNaturalLateRefusal(FarewellReadinessRoute route, int calls, bool attempted)
        {
            float storedDeadline = route.Deadline, observationBy = Time.realtimeSinceStartup + 2f;
            Assert.That(Time.unscaledTime, Is.LessThanOrEqualTo(storedDeadline),
                "MISSING an on-time sample before the first natural late update.");
            while (Time.unscaledTime <= storedDeadline)
            {
                Assert.That(ReadFarewellField<string>(route.Stage, "farewellFailure"), Is.Null,
                    "A preexisting/early refusal is not the original deadline witness.");
                AssertNoNewFarewellTake(route, calls, attempted);
                int precedingFrame = Time.frameCount;
                yield return null; // Resume after ordinary Update; never invoke Tick or alter a clock.
                Assert.That(Time.frameCount, Is.EqualTo(precedingFrame + 1),
                    "MISSING consecutive natural updates; an eventual late sample is not first-late proof.");
                Assert.That(Time.realtimeSinceStartup, Is.LessThanOrEqualTo(observationBy),
                    "MISSING deadline witness within the unchanged observation bound.");
            }
            // This is the FIRST consecutive resumed frame whose original clock is > the stored deadline.
            Assert.That(ReadFarewellField<float>(route.Stage, "farewellAtGoodbyeBy"), Is.EqualTo(storedDeadline));
            Assert.That(ReadFarewellField<string>(route.Stage, "farewellFailure"), Does.Contain("readiness exceeded1.5s"));
            Assert.That(ReadFarewellField<string>(route.Stage, "farewellTerminalRefusal"),
                Does.Contain("frame=" + Time.frameCount + " delta="));
            AssertNoNewFarewellTake(route, calls, attempted);
            AssertOriginalFarewellLeasesReleased(route); // BEFORE any Skip can supply cleanup.
        }

        [UnityTest, Category("FarewellProvidedReadiness")]
        public IEnumerator GroundedFarewell_ReadinessBothWalkStopCallsOnlyOnce()
        {
            yield return BeginProvidedFarewellReadiness();
            var route = farewellReadinessRoute;
            yield return WaitForFarewellAuthorization(route);
            AssertCapturedFarewellBranch(route, false, false);
            yield return ObserveFarewellIdleBeforeCut(route);
            AssertCapturedFarewellBranch(route, false, false);
        }

        [UnityTest, Category("FarewellProvidedReadiness")]
        public IEnumerator GroundedFarewell_ReadinessMixedIdleDoesNotResetTheIdleEndpoint()
        {
            yield return BeginProvidedFarewellReadiness(.15f);
            var route = farewellReadinessRoute;
            yield return WaitForFarewellAuthorization(route);
            var a = ReadFarewellField(route.Stage, "farewellIdleA");
            var b = ReadFarewellField(route.Stage, "farewellIdleB");
            bool firstIdle = ReadFarewellField<bool>(a, "WasIdle"), secondIdle = ReadFarewellField<bool>(b, "WasIdle");
            Assert.That(firstIdle != secondIdle, Is.True, "MISSING ordinary mixed branch; no forced clips.");
            AssertCapturedFarewellBranch(route, firstIdle, secondIdle);
            float initialIdle = ReadFarewellField<float>(firstIdle ? a : b, "Phase");
            yield return ObserveFarewellIdleBeforeCut(route);
            Assert.That(ReadFarewellField<float>(route.Stage, firstIdle ? "farewellIdlePhaseA" : "farewellIdlePhaseB"),
                Is.GreaterThan(initialIdle), "The no-op Idle advances rather than resetting.");
        }

        [UnityTest, Category("FarewellProvidedReadiness")]
        public IEnumerator GroundedFarewell_ReadinessBothIdleStillRequiresNewEvaluations()
        {
            yield return BeginProvidedFarewellReadiness(.35f);
            var route = farewellReadinessRoute;
            yield return WaitForFarewellAuthorization(route);
            AssertCapturedFarewellBranch(route, true, true);
            yield return ObserveFarewellIdleBeforeCut(route);
            AssertCapturedFarewellBranch(route, true, true);
        }

        [UnityTest, Category("FarewellProvidedReadiness")]
        public IEnumerator GroundedFarewell_ReadinessDoesNotRestartAnExistingGlobalStopExit()
        {
            yield return BeginProvidedFarewellReadiness(.1f);
            var route = farewellReadinessRoute;
            Animator observed = null; float priorPhase = 0f;
            float by = Time.realtimeSinceStartup + 2f;
            while (observed == null && Time.realtimeSinceStartup < by && Time.unscaledTime <= route.Deadline)
            {
                foreach (var body in new[] { route.First, route.Second })
                {
                    var animator = ReadFarewellAnimator(body);
                    if (animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).IsName("WalkStop")
                        && animator.GetNextAnimatorStateInfo(0).IsName("Idle"))
                    {
                        var source = animator.GetCurrentAnimatorClipInfo(0);
                        var incoming = animator.GetNextAnimatorClipInfo(0);
                        var basis = Resources.Load<AnimatorOverrideController>("Animation/GamesimHumanoid").runtimeAnimatorController;
                        var stop = basis.animationClips.Single(clip => clip != null && clip.name == "WalkStop");
                        Assert.That(source.Length == 1 && incoming.Length == 1 && source[0].clip == stop
                            && incoming[0].clip == ReadFarewellIdleClip(animator)
                            && source[0].weight >= 0f && source[0].weight <= 1f
                            && incoming[0].weight >= 0f && incoming[0].weight <= 1f, Is.True,
                            "Exact actual global stop/Idle clip objects, not names or a fabricated crossing.");
                        observed = animator; priorPhase = animator.GetAnimatorTransitionInfo(0).normalizedTime; break;
                    }
                }
                if (observed == null) yield return null;
            }
            Assert.That(observed, Is.Not.Null, "MISSING genuine global transition witness; no local transition is manufactured.");
            AssertNoFarewellPreparation(route);
            yield return null;
            if (observed.IsInTransition(0))
                Assert.That(observed.GetAnimatorTransitionInfo(0).normalizedTime, Is.GreaterThanOrEqualTo(priorPhase),
                    "The original crossing is not restarted.");
            yield return WaitForFarewellAuthorization(route);
            yield return ObserveFarewellIdleBeforeCut(route);
        }

        [UnityTest, Category("FarewellProvidedReadiness")]
        public IEnumerator GroundedFarewell_ReadinessOrdinaryUpdatesNeverRetryPreparation()
        {
            yield return BeginProvidedFarewellReadiness();
            var route = farewellReadinessRoute;
            yield return WaitForFarewellAuthorization(route);
            int calls = ReadFarewellField<int>(route.Stage, "farewellIdleCalls");
            bool attempted = ReadFarewellField<bool>(route.Stage, "farewellIdleAttemptUsed");
            int frames = 0; float by = Time.realtimeSinceStartup + .5f;
            while (Time.realtimeSinceStartup < by)
            {
                Assert.That(ReadFarewellField<int>(route.Stage, "farewellIdleCalls"), Is.EqualTo(calls));
                Assert.That(ReadFarewellField<bool>(route.Stage, "farewellIdleAttemptUsed"), Is.EqualTo(attempted));
                AssertFarewellSave(route); frames++; yield return null;
            }
            Assert.That(frames, Is.GreaterThanOrEqualTo(2), "Ordinary updates, not private Tick or synchronous reentry.");
        }

        [UnityTest, Category("FarewellReadinessControlledNegative")]
        public IEnumerator GroundedFarewell_ReadinessRefusesSecondForeignOwnersBeforeCommit()
        {
            for (int cue = 0; cue < 4; cue++)
            {
                yield return BeginProvidedFarewellReadiness();
                var route = farewellReadinessRoute;
                yield return WaitForFarewellBeforeCommit(route);
                if (cue < 2)
                {
                    var reaction = cue == 0 ? CharacterPresentation.Reaction.Furious : CharacterPresentation.Reaction.Shocked;
                    Assert.That(route.Second.Supports(reaction), Is.True);
                    route.Second.React(reaction); // Controlled body API, NOT a season activity.
                }
                else if (cue == 2) Assert.That(route.Second.MakeGesture(CharacterPresentation.Gesture.Shrug), Is.True);
                else route.Second.SetActivity(CharacterPresentation.BodyActivity.Cooking);
                Assert.That(PendingOrCurrentFarewellForeign(route.Second, cue), Is.True, "Actual accepted foreign input required.");
                int samples = 0; float by = Time.realtimeSinceStartup + .25f;
                while (Time.realtimeSinceStartup < by)
                {
                    // A natural handoff can lose the positive witness for one frame.
                    // Observe it passively inside the ORIGINAL window; it is not cleanup.
                    PendingOrCurrentFarewellForeign(route.Second, cue);
                    AssertNoFarewellPreparation(route); samples++; yield return null;
                }
                Assert.That(samples, Is.GreaterThan(0));
                bool ownerBeforeSkip = PendingOrCurrentFarewellForeign(route.Second, cue);
                Assert.That(ownerBeforeSkip, Is.True,
                    "MISSING exact pre-Skip foreign owner; an unobserved handoff or cue progression cannot be blamed on cleanup.");
                int cleanupFrame = Time.frameCount;
                director.SkipWalkOut();
                Assert.That(Time.frameCount, Is.EqualTo(cleanupFrame), "Compare the synchronous cleanup in the SAME frame.");
                bool ownerAfterSkip = PendingOrCurrentFarewellForeign(route.Second, cue);
                Assert.That(ownerAfterSkip, Is.EqualTo(ownerBeforeSkip),
                    "Same-frame cleanup lost the exact foreign owner witnessed immediately before Skip.");
                AssertFarewellSave(route); yield return null;
            }
        }

        [UnityTest, Category("FarewellReadinessControlledNegative")]
        public IEnumerator GroundedFarewell_ReadinessRefusesPendingAndCompletedLegitimateDress()
        {
            yield return BeginProvidedFarewellReadiness();
            var route = farewellReadinessRoute;
            yield return WaitForFarewellBeforeCommit(route);
            var modular = CharacterBodySource.Provider as IModularCharacterBodyProvider;
            Assert.That(modular?.Catalog, Is.Not.Null, "MISSING actual supported wardrobe catalog; no fake provider.");
            var draft = director.Snapshot.Find(route.Second.CharacterId).Clone();
            draft.appearance = modular.Catalog.Materialize(draft.appearance);
            var item = modular.Catalog.Items.FirstOrDefault(value => value.Fits(draft.appearance.bodyId)
                && !AppearanceEditing.IsCharacterSlot(value.Slot)
                && !AppearanceEditing.Outfit(draft.appearance).wardrobe.Any(worn => value.Matches(worn.itemId)));
            Assert.That(item, Is.Not.Null, "MISSING supported alternative wardrobe item.");
            AppearanceEditing.Wear(draft.appearance, item, modular.Catalog);
            Assert.That(draft.appearance.TryValidate(out var invalid), Is.True, invalid);
            var oldIdentity = ReadFarewellBodyIdentity(route.Second);
            var oldVisual = (Transform)oldIdentity[0]; var oldAnimator = (Animator)oldIdentity[2];
            string oldIds = string.Join("/", oldIdentity.Select(value => value is Object asset
                ? asset.GetEntityId().ToString() : value?.ToString() ?? "null"));
            CharacterPresentation.Dress(route.Second.gameObject, draft, Color.white); // Copied recipe, never the saved contestant.
            Assert.That(route.Second.IsChangingOutfit, Is.True, "Genuine pending build is required.");
            yield return null;
            AssertNoFarewellPreparation(route);
            float by = Time.realtimeSinceStartup + CharacterPresentation.AssemblyTimeoutSeconds + 1f;
            while (route.Second != null && Time.realtimeSinceStartup < by)
            {
                var current = route.Second.VisualRoot != null
                    ? route.Second.VisualRoot.GetComponentInChildren<Animator>(true) : null;
                if (!route.Second.IsChangingOutfit && route.Second.VisualRoot != oldVisual
                    && current != null && current.isInitialized && current.isHuman
                    && current.avatar != null && current.avatar.isValid) break;
                AssertNoFarewellPreparation(route); yield return null;
            }
            Assert.That(route.Second != null && !route.Second.IsChangingOutfit && route.Second.VisualRoot != oldVisual, Is.True,
                "MISSING completed genuine handover; pending-only is not replacement acceptance.");
            var newAnimator = ReadFarewellAnimator(route.Second);
            Assert.That(newAnimator, Is.Not.SameAs(oldAnimator));
            Assert.That(newAnimator.runtimeAnimatorController != null && newAnimator.avatar != null, Is.True);
            var newIdentity = ReadFarewellBodyIdentity(route.Second);
            Assert.That(newIdentity[1], Is.Not.SameAs(oldIdentity[1]), "Actual provided root handover, not only a pending wardrobe.");
            TestContext.WriteLine("Completed public Dress identity Visual/ProvidedRoot/Animator/Controller/Avatar/Build/Revision old="
                + oldIds + " new=" + string.Join("/", newIdentity.Select(value => value is Object asset
                    ? asset.GetEntityId().ToString() : value?.ToString() ?? "null")));
            AssertNoFarewellPreparation(route);
        }

        [UnityTest, Category("FarewellReadinessControlledNegative")]
        public IEnumerator GroundedFarewell_ReadinessOldOwnerCannotReleaseAFreshSameIdToken()
        {
            yield return BeginProvidedFarewellReadiness();
            var route = farewellReadinessRoute;
            yield return WaitForFarewellBeforeCommit(route);
            var motion = route.Second.GetComponent<HouseNpcMotion>();
            route.Owner.ReleaseCeremonyActor(route.Second.CharacterId);
            Assert.That(motion.LeaseId, Is.Null);
            Assert.That(route.Owner.JoinCeremonyStage(route.Second.CharacterId, route.SecondPlace, out var reason), Is.True, reason);
            string fresh = motion.LeaseId;
            Assert.That(fresh != null && fresh != route.SecondToken && motion.BoundNpcId == route.Second.CharacterId
                && route.Owner.CeremonyActorHolds(route.Second.CharacterId)
                && route.Owner.CeremonyPlace(route.Second.CharacterId) == route.SecondPlace, Is.True);
            yield return null; // Original farewell ownership check/cleanup runs naturally.
            AssertNoFarewellPreparation(route);
            Assert.That(motion.LeaseId, Is.EqualTo(fresh), "Stale captured cleanup must not release the genuine replacement.");
            Assert.That(route.Owner.CeremonyActorHolds(route.Second.CharacterId), Is.True);
        }

        [UnityTest, Category("FarewellReadinessControlledNegative")]
        public IEnumerator GroundedFarewell_ReadinessAfterFirstBoundaryIsOnceOnlyAndDoesNotDriveSecond()
        {
            // Cooking, real fresh token, controlled exception and public skip/reentry are four
            // labelled controlled scenarios inside one leaf; none is an actual native exception.
            for (int scenario = 0; scenario < 4; scenario++)
            {
                yield return BeginProvidedFarewellReadiness();
                var route = farewellReadinessRoute;
                bool fired = false, joined = false, rearmed = false, capturedBothStops = false; string fresh = null; int callsAtBoundary = -1;
                int selected = scenario;
                try
                {
                    Assert.That(director.ArmGoodbyeFarewellAfterFirstWriteForTests((first, second, owner, pa, pb) =>
                    {
                        fired = true; callsAtBoundary = ReadFarewellField<int>(route.Stage, "farewellIdleCalls");
                        capturedBothStops = CapturedFarewellWalkStops(route);
                        if (!capturedBothStops) return; // Outside assertion must FAIL; runtime catches callback exceptions.
                        if (selected == 0) second.SetActivity(CharacterPresentation.BodyActivity.Cooking);
                        else if (selected == 1)
                        {
                            owner.ReleaseCeremonyActor(second.CharacterId);
                            joined = owner.JoinCeremonyStage(second.CharacterId, pb, out _);
                            fresh = second.GetComponent<HouseNpcMotion>().LeaseId;
                        }
                        else if (selected == 2) throw new System.InvalidOperationException("controlled readiness boundary");
                        else
                        {
                            rearmed = director.ArmGoodbyeFarewellAfterFirstWriteForTests((a, b, o, ap, bp) => { });
                            director.SkipWalkOut();
                        }
                    }), Is.True);
                    float by = Time.realtimeSinceStartup + 2f;
                    while (!fired && Time.realtimeSinceStartup < by && Time.unscaledTime <= route.Deadline)
                    { AssertFarewellSave(route); yield return null; }
                    Assert.That(fired, Is.True, "MISSING actual FIRST returned WalkStop write; next-frame injection is not equivalent.");
                    Assert.That(capturedBothStops, Is.True, "MISSING captured SECOND WalkStop; an Idle no-op cannot prove a refused write.");
                    Assert.That(callsAtBoundary, Is.EqualTo(1));
                    Assert.That(ReadFarewellField<bool>(route.Stage, "farewellIdleAttemptUsed"), Is.True);
                    AssertNoNewFarewellTake(route, 1, true); // A genuine captured SECOND WalkStop remains unwritten.
                    if (selected == 0) Assert.That(route.Second.Activity, Is.EqualTo(CharacterPresentation.BodyActivity.Cooking));
                    else if (selected == 1)
                    {
                        Assert.That(joined && fresh != null && fresh != route.SecondToken, Is.True);
                        Assert.That(route.Second.GetComponent<HouseNpcMotion>().BoundNpcId, Is.EqualTo(route.Second.CharacterId));
                        Assert.That(route.Second.GetComponent<HouseNpcMotion>().LeaseId, Is.EqualTo(fresh));
                        Assert.That(route.Owner.CeremonyActorHolds(route.Second.CharacterId)
                            && route.Owner.CeremonyPlace(route.Second.CharacterId) == route.SecondPlace, Is.True);
                    }
                    else if (selected == 2)
                        Assert.That(ReadFarewellField<string>(route.Stage, "farewellFailure"),
                            Does.Contain("owned Idle preparation failure: InvalidOperationException"));
                    else Assert.That(rearmed, Is.False, "Clear-before-invoke does not permit rearming an authorized stage.");
                    AssertFarewellSave(route);
                    yield return null;
                    Assert.That(ReadFarewellField<int>(route.Stage, "farewellIdleCalls"), Is.EqualTo(1), "No retry after another ordinary update.");
                }
                finally { director.ClearGoodbyeFarewellAfterFirstWriteForTests(); }
            }
        }

        [UnityTest, Category("FarewellReadinessControlledNegative")]
        public IEnumerator GroundedFarewell_ReadinessAfterBothCallsPreservesANewerCue()
        {
            yield return BeginProvidedFarewellReadiness();
            var route = farewellReadinessRoute;
            yield return WaitForFarewellAuthorization(route);
            AssertCapturedFarewellBranch(route, false, false); // EXACTLY TWO actual normally returned writes.
            Assert.That(ReadFarewellField<bool>(route.Stage, "farewellIdlePrepared"), Is.True,
                "A stamped/interrupted authorization cannot qualify AFTER BOTH.");
            AssertNoNewFarewellTake(route, 2, true); // This is pre-pair, not playing-pair cancellation.
            Assert.That(route.Second.Supports(CharacterPresentation.Reaction.Furious), Is.True);
            route.Second.React(CharacterPresentation.Reaction.Furious); // Next-frame AFTER BOTH, not AFTER FIRST.
            Assert.That(PendingOrCurrentFarewellForeign(route.Second, 0), Is.True);
            int samples = 0; float by = Time.realtimeSinceStartup + .25f;
            while (Time.realtimeSinceStartup < by && PendingOrCurrentFarewellForeign(route.Second, 0))
            {
                AssertNoNewFarewellTake(route, 2, true);
                samples++; yield return null;
            }
            Assert.That(samples, Is.GreaterThan(0), "MISSING ordinary update with the exact newer owner.");
            Assert.That(PendingOrCurrentFarewellForeign(route.Second, 0), Is.True,
                "MISSING surviving exact cue at cleanup; natural expiry is not a substitute witness.");
            AssertNoNewFarewellTake(route, 2, true);
            director.SkipWalkOut();
            Assert.That(PendingOrCurrentFarewellForeign(route.Second, 0), Is.True);
            AssertNoNewFarewellTake(route, 2, true);
        }

        [UnityTest, Category("FarewellReadinessControlledNegative")]
        public IEnumerator GroundedFarewell_ReadinessOriginalDeadlineRefusesOnTheFirstNaturalLateTick()
        {
            yield return BeginProvidedFarewellReadiness();
            var route = farewellReadinessRoute;
            yield return WaitForFarewellBeforeCommit(route);
            route.Second.SetActivity(CharacterPresentation.BodyActivity.Cooking);
            yield return ObserveFarewellFirstNaturalLateRefusal(route, 0, false);
        }

        [UnityTest, Category("FarewellReadinessControlledNegative")]
        public IEnumerator GroundedFarewell_ReadinessSkipAndReducedMotionKeepTheirOriginalWindows()
        {
            for (int window = 0; window < 3; window++)
            for (int reduced = 0; reduced < 2; reduced++)
            {
                yield return BeginProvidedFarewellReadiness();
                var route = farewellReadinessRoute;
                if (window == 0) yield return WaitForFarewellBeforeCommit(route);
                else
                {
                    yield return WaitForFarewellAuthorization(route);
                    Assert.That(ReadFarewellField<bool>(route.Stage, "farewellIdlePrepared"), Is.True);
                    if (window == 2)
                    {
                        yield return WaitForFarewellEndpoint(() => director.GoodbyeFarewell != null, 2f,
                            "MISSING actual during-take cancellation witness", route.First.CharacterId, route.Second.CharacterId);
                        var enteringPair = director.GoodbyeFarewell;
                        float entryBy = ReadFarewellField<float>(enteringPair, "firedAt") + .5f;
                        bool actualBothTakes = false;
                        while (enteringPair.Stage == FarewellPair.Phase.Playing && Time.unscaledTime <= entryBy)
                        {
                            actualBothTakes = route.First.TryReadFarewell(out var firstTake) && firstTake.InTake
                                && route.Second.TryReadFarewell(out var secondTake) && secondTake.InTake;
                            if (actualBothTakes) break;
                            yield return null;
                        }
                        Assert.That(enteringPair.Stage == FarewellPair.Phase.Playing && actualBothTakes, Is.True,
                            "MISSING genuinely playing actual take; an already-cancelled/pending pair cannot substitute.");
                    }
                }
                int calls = ReadFarewellField<int>(route.Stage, "farewellIdleCalls");
                bool attempted = ReadFarewellField<bool>(route.Stage, "farewellIdleAttemptUsed");
                var pair = director.GoodbyeFarewell;
                if (reduced == 1)
                {
                    route.Second.SetReducedMotion(true);
                    if (window < 2)
                    {
                        Assert.That(pair, Is.Null, "Pre/post preparation window must be before an actual pair.");
                        yield return ObserveFarewellFirstNaturalLateRefusal(route, calls, attempted);
                        // Both captured leases are already released here BEFORE Skip can supply cleanup.
                    }
                    else
                    {
                        Assert.That(pair != null && pair.Stage == FarewellPair.Phase.Playing, Is.True);
                        float takeBy = Mathf.Min(ReadFarewellField<float>(pair, "firedAt") + 2f, route.GoodbyeEnd);
                        while (pair.Stage == FarewellPair.Phase.Playing && Time.unscaledTime <= takeBy)
                            yield return null;
                        Assert.That(pair.Stage, Is.EqualTo(FarewellPair.Phase.Cancelled),
                            "Reduced motion must naturally cancel the captured playing pair BEFORE Skip.");
                        Assert.That(pair.ReleaseReason, Does.Contain("standing owner/body/clock/feet changed"));
                        bool neutral = false;
                        while (Time.unscaledTime <= takeBy)
                        {
                            neutral = route.First.TryReadFarewell(out var firstNeutral) && firstNeutral.Idle
                                && route.Second.TryReadFarewell(out var secondNeutral) && secondNeutral.Idle;
                            if (neutral) break;
                            yield return null;
                        }
                        Assert.That(neutral, Is.True, "MISSING actual neutral Idle readback BEFORE Skip.");
                        foreach (var body in new[] { route.First, route.Second })
                        {
                            Assert.That(ReadFarewellAnimator(body).GetBool(Animator.StringToHash("ReactFarewell")), Is.False);
                            Assert.That(ReadFarewellField<int>(body, "pendingReaction"),
                                Is.Not.EqualTo((int)CharacterPresentation.Reaction.Farewell));
                        }
                        Assert.That(ReadFarewellField<int>(route.Stage, "farewellIdleCalls"), Is.EqualTo(calls));
                        AssertFarewellSave(route);
                        // Pair.Cancel releases only its own cue. Ceremony lease release is not attributed to it.
                    }
                }
                director.SkipWalkOut();
                AssertOriginalFarewellLeasesReleased(route);
                Assert.That(director.GoodbyeFarewell, Is.Null);
                AssertFarewellSave(route); yield return null;
            }
        }

        [UnityTest, Category("FarewellReadinessControlledNegative")]
        public IEnumerator GroundedFarewell_ReadinessDisabledPresentationsCannotWriteOrEraseANewerOwner()
        {
            for (int afterFirst = 0; afterFirst < 2; afterFirst++)
            for (int endpoint = 0; endpoint < 2; endpoint++)
            {
                yield return BeginProvidedFarewellReadiness();
                var route = farewellReadinessRoute;
                var disabled = endpoint == 0 ? route.First : route.Second;
                var identity = ReadFarewellBodyIdentity(disabled);
                var liveAnimator = ReadFarewellAnimator(disabled);
                bool fired = false, capturedBothStops = false; int callsAtBoundary = -1;
                try
                {
                    if (afterFirst == 0)
                    {
                        yield return WaitForFarewellBeforeCommit(route);
                        AssertActualFarewellWalkStopBeforeControl(route.First);
                        AssertActualFarewellWalkStopBeforeControl(route.Second);
                        disabled.enabled = false; // Controlled component lifecycle, not a root/Animator disable.
                    }
                    else
                    {
                        Assert.That(director.ArmGoodbyeFarewellAfterFirstWriteForTests((first, second, owner, pa, pb) =>
                        {
                            fired = true;
                            callsAtBoundary = ReadFarewellField<int>(route.Stage, "farewellIdleCalls");
                            capturedBothStops = CapturedFarewellWalkStops(route);
                            if (capturedBothStops) disabled.enabled = false; // The activation input is the ONLY refusal cause.
                        }), Is.True);
                        float by = Time.realtimeSinceStartup + 2f;
                        while (!fired && Time.realtimeSinceStartup < by && Time.unscaledTime <= route.Deadline)
                            yield return null;
                        Assert.That(fired && capturedBothStops, Is.True,
                            "MISSING actual FIRST write/BOTH captured WalkStops; an Idle no-op is not activation coverage.");
                        Assert.That(callsAtBoundary, Is.EqualTo(1));
                    }
                    Assert.That(disabled.isActiveAndEnabled, Is.False);
                    Assert.That(liveAnimator.isActiveAndEnabled && liveAnimator.isInitialized
                        && liveAnimator.gameObject.activeInHierarchy && disabled.VisualRoot.gameObject.activeInHierarchy, Is.True,
                        "The same Animator/root stays live while ONLY the presentation component is disabled.");
                    Assert.That(ReadFarewellBodyIdentity(disabled), Is.EqualTo(identity));
                    if (afterFirst == 0)
                        yield return ObserveFarewellFirstNaturalLateRefusal(route, 0, false);
                    else
                    {
                        AssertNoNewFarewellTake(route, 1, true);
                        Assert.That(ReadFarewellField<string>(route.Stage, "farewellFailure"),
                            Does.Contain("Idle preparation interrupted after first endpoint"));
                        AssertOriginalFarewellLeasesReleased(route);
                        yield return null;
                        AssertNoNewFarewellTake(route, 1, true); // No SECOND or retry on ordinary updates.
                    }
                    // Establish activation-only refusal FIRST, then observe a genuine newer foreign owner
                    // through old cleanup. This cue cannot mask a missing component activation guard.
                    Assert.That(route.Second.Supports(CharacterPresentation.Reaction.Shocked), Is.True);
                    route.Second.React(CharacterPresentation.Reaction.Shocked);
                    Assert.That(PendingOrCurrentFarewellForeign(route.Second, 1), Is.True);
                    yield return null;
                    Assert.That(PendingOrCurrentFarewellForeign(route.Second, 1), Is.True);
                    AssertNoNewFarewellTake(route, afterFirst == 0 ? 0 : 1, afterFirst != 0);
                    director.SkipWalkOut();
                    Assert.That(PendingOrCurrentFarewellForeign(route.Second, 1), Is.True,
                        "Captured cleanup must preserve the exact newer cue, not an arbitrary reaction.");
                    AssertFarewellSave(route);
                }
                finally
                {
                    director.ClearGoodbyeFarewellAfterFirstWriteForTests();
                    if (disabled != null) disabled.enabled = true; // Fixture cleanup only; never retry this transaction.
                }
            }
        }

        [UnityTest, Category("FarewellProvidedReadiness")]
        public IEnumerator GroundedFarewell_ReadinessKeepsActualCameraTakeAndWholeGoodbye()
        {
            yield return BeginProvidedFarewellReadiness();
            var route = farewellReadinessRoute;
            yield return WaitForFarewellAuthorization(route);
            yield return ObserveFarewellIdleBeforeCut(route);
            yield return WaitForFarewellEndpoint(() => director.GoodbyeFarewell != null, 2f,
                "MISSING actual provided take after camera", route.First.CharacterId, route.Second.CharacterId);
            var pair = director.GoodbyeFarewell;
            Assert.That(pair.FiredFrame, Is.GreaterThan(ReadFarewellField<int>(route.Stage, "farewellCutFrame")));
            yield return ObserveFarewellToIdle(pair, route.GoodbyeEnd, route.Saved);
            // This strict existing observer includes actual during-take projection/palms and
            // unchanged .001m foot/root/.05degree heading. A failure stays failure, NOT readiness planting.
            Assert.That(ReadFarewellField<float>(route.Stage, "goodbyeUntil"), Is.EqualTo(route.GoodbyeEnd));
        }
#endif

        private void ReleaseProvidedFarewellFixture(bool originalCleanupCompleted)
        {
#if UNITY_EDITOR && UNITY_INCLUDE_TESTS && !GAMESIM_UMA
            var owned = providedFarewellFixtureProvider;
            if (owned == null) return; // Generic and UMA paths never acquired this readiness lease.
            bool unregistered = false;
            try
            {
                CharacterBodySource.Unregister(owned); // Exact instance even if an unrelated provider is now top.
                unregistered = true;
            }
            catch (System.Exception failure)
            {
                RetainProvidedFarewellReleaseFailure(providedFarewellFixtureReleaseFailures, "unregister", failure);
            }
            owned.ReleaseOwnedClones(); // Each owned clone attempted independently, after registry removal.
            try
            {
                Assert.That(CharacterBodySource.Provider, Is.Null,
                    "NoUMA fixture must restore its observed empty registry; never delete an unrelated provider.");
            }
            catch (System.Exception failure)
            {
                RetainProvidedFarewellReleaseFailure(providedFarewellFixtureReleaseFailures, "registry-restoration", failure);
            }
            providedFarewellFixtureReleaseSucceeded = originalCleanupCompleted && unregistered
                && owned.LiveOwnedCloneCount == 0 && providedFarewellFixtureReleaseFailures.Count == 0;
            if (unregistered && owned.LiveOwnedCloneCount == 0) providedFarewellFixtureProvider = null;
            try
            {
                TestContext.WriteLine("[Gamesim] Farewell fixture release completedOriginalCleanup=" + originalCleanupCompleted
                    + " unregistered=" + unregistered + " liveOwnedClones=" + owned.LiveOwnedCloneCount
                    + " retainedReleaseFailures=" + providedFarewellFixtureReleaseFailures.Count
                    + " releaseSucceeded=" + providedFarewellFixtureReleaseSucceeded
                    + (originalCleanupCompleted ? "" : " INTERRUPTED_OR_ACTIVE_FAILURE_NOT_SUCCESS"));
            }
            catch (System.Exception evidenceFailure)
            {
                providedFarewellFixtureReleaseSucceeded = false;
                providedFarewellFixtureReleaseFailures.Add(
                    new System.InvalidOperationException("Farewell fixture release summary evidence", evidenceFailure));
            }
            if (originalCleanupCompleted && providedFarewellFixtureReleaseFailures.Count != 0)
                throw new System.AggregateException("Farewell fixture release failed; every owned release was attempted.",
                    providedFarewellFixtureReleaseFailures);
            // An interrupted iterator can be cancellation/disposal, not necessarily an original exception.
            // Keep secondary failures and releaseSucceeded=false without masking an active original exception.
#endif
        }

        private IEnumerator BeginFarewellFixture()
        {
            yield return InstallStagedSeason(52, AtEviction);
            yield return PlayUntilTheCeremony();
            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing,
                EpisodeDirector.SummonsHardSeconds(CeremonyPace.Suspenseful) + 6f, "the genuine staged eviction reaches its card");
            string leaving = director.DepartingId;
            string staying = director.Snapshot.nominees.Single(id => id != leaving);
            var vote = SceneComponents<VoteReveal>().Single();
            vote.SkipToResult();
            vote.Cancel();
            yield return null;
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Goodbye));
            yield return WaitForFarewellEndpoint(() => director.GoodbyeFarewell != null
                || Presentation(HouseguestBody(leaving)).IsPrimitiveBody && Presentation(HouseguestBody(staying)).IsPrimitiveBody
                    && Presentation(HouseguestBody(leaving)).LastReaction == CharacterPresentation.Reaction.Farewell
                    && Presentation(HouseguestBody(staying)).LastReaction == CharacterPresentation.Reaction.Farewell,
                2f, "real owned pair or separately labeled clone diagnostic", leaving, staying);
            if (director.GoodbyeFarewell != null)
                Assert.That(director.GoodbyeFarewell.Stage, Is.EqualTo(FarewellPair.Phase.Playing), "Cancel controls act while the actual pair is playing.");
        }
    }
}
