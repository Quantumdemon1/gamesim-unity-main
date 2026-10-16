#if UNITY_EDITOR
using System.Collections;
using Gamesim.Presentation;
using Gamesim.Tests.PlayMode;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Uma.Tests
{
    /// <summary>
    /// The farewell readiness's three Idle/WalkStop decisions, tested directly on the UMA cast in the real
    /// staged eviction (lead ruling, 2026-10-16). On the UMA cast the evicted ordinarily stands into WalkStop
    /// and the survivor into Idle, so whether the cast reaches both WalkStop, both Idle, or a WalkStop exit
    /// already crossing when the director authorizes depends on the walk's timing; the readiness tests that
    /// wait for those combinations to occur naturally are diagnostics (Gamesim.Uma.DiagnosticsPlayModeTests).
    /// These tests do not wait: each puts the two principals' Animators into its combination on the frame
    /// before the director authorizes the Idle preparation, then lets the unchanged director decide. The
    /// forcing is the test's; the decision, every Animator write and every refusal are the product's.
    /// The fixture is EpisodePlayModeTests, composed (its own set-up and tear-down), and the route and its
    /// witnesses are that fixture's public readiness helper, shared with the shipping readiness tests.
    /// </summary>
    public sealed class UmaFarewellReadinessDecisionPlayModeTests
    {
        private static readonly int IdleState = Animator.StringToHash("Idle"), WalkStopState = Animator.StringToHash("WalkStop");
        private const float WalkStopExitTime = .55f; // GamesimHumanoid.controller: WalkStop -> Idle, exit time .55, .25 s.
        private EpisodePlayModeTests episode;

        [UnitySetUp]
        public IEnumerator LoadTheEpisode()
        {
            episode = new EpisodePlayModeTests();
            yield return episode.LoadIsolatedEpisode();
        }

        [UnityTearDown]
        public IEnumerator UnloadTheEpisode()
        {
            if (episode != null) yield return episode.CleanUpIsolatedEpisode();
            episode = null;
        }

        private static T Stage<T>(EpisodePlayModeTests.FarewellReadinessRoute route, string field)
            => EpisodePlayModeTests.ReadFarewellField<T>(route.Stage, field);

        // Test-forced, never a product path: the state is requested and evaluated at once, so it is the
        // current, nontransitioning endpoint when the director's next Update reads it.
        private static Animator Force(CharacterPresentation body, int state, float normalizedTime)
        {
            var animator = EpisodePlayModeTests.ReadFarewellAnimator(body);
            animator.Play(state, 0, normalizedTime);
            animator.Update(0f);
            return animator;
        }

        private static void AssertEndpoint(CharacterPresentation body, bool idle)
        {
            var animator = EpisodePlayModeTests.ReadFarewellAnimator(body);
            var state = animator.GetCurrentAnimatorStateInfo(0);
            var clips = animator.GetCurrentAnimatorClipInfo(0);
            var expected = idle ? EpisodePlayModeTests.ReadFarewellIdleClip(animator)
                : EpisodePlayModeTests.ReadFarewellSelectedClip(animator, "WalkStop");
            Assert.That(!animator.IsInTransition(0) && state.IsName(idle ? "Idle" : "WalkStop")
                && clips.Length == 1 && clips[0].clip == expected && clips[0].weight > 0f, Is.True,
                body.CharacterId + " does not stand in the forced " + (idle ? "Idle" : "WalkStop")
                    + " on its exact clip before the director's next Update.");
        }

        private static void LogMargin(EpisodePlayModeTests.FarewellReadinessRoute route, string at)
            => TestContext.WriteLine(at + ": frame " + Time.frameCount + ", " + (route.Deadline - Time.unscaledTime).ToString("0.000")
                + " s before the original readiness deadline.");

        // BOTH WalkStop: one attempt, and each endpoint is written exactly once - both bodies cross from
        // their WalkStop into Idle on the next evaluation, and neither crossing is ever begun again.
        [UnityTest, Category("FarewellReadinessDecision")]
        public IEnumerator BothWalkStopEndpointsAreEachWrittenOnceAndNeverAgain()
        {
            yield return episode.BeginProvidedFarewellReadiness();
            var route = episode.FarewellReadiness;
            yield return episode.WaitForFarewellBeforeCommit(route);
            var animators = new[] { Force(route.First, WalkStopState, 0f), Force(route.Second, WalkStopState, 0f) };
            AssertEndpoint(route.First, false); AssertEndpoint(route.Second, false);
            LogMargin(route, "Both forced into WalkStop");
            yield return episode.WaitForFarewellAuthorization(route);
            episode.AssertCapturedFarewellBranch(route, false, false); // one attempt, exactly two writes
            yield return null; // The writes land on the evaluation after the director's Update.
            var prior = new[] { -1f, -1f };
            int crossingFrames = 0; float by = Time.realtimeSinceStartup + 2f;
            while ((animators[0].IsInTransition(0) || animators[1].IsInTransition(0)) && Time.realtimeSinceStartup < by)
            {
                for (int i = 0; i < 2; i++)
                {
                    bool crossing = animators[i].IsInTransition(0);
                    if (crossingFrames == 0)
                        Assert.That(crossing && animators[i].GetCurrentAnimatorStateInfo(0).normalizedTime < WalkStopExitTime, Is.True,
                            "Endpoint " + i + ": the director's write, not the WalkStop's own exit, must cross it into Idle on the next evaluation.");
                    if (!crossing) { prior[i] = float.MaxValue; continue; }
                    Assert.That(prior[i], Is.Not.EqualTo(float.MaxValue), "Endpoint " + i + " crossed into Idle a second time.");
                    Assert.That(animators[i].GetCurrentAnimatorStateInfo(0).IsName("WalkStop")
                        && animators[i].GetNextAnimatorStateInfo(0).IsName("Idle"), Is.True, "Endpoint " + i + " crosses WalkStop into Idle.");
                    float phase = animators[i].GetAnimatorTransitionInfo(0).normalizedTime;
                    Assert.That(phase, Is.GreaterThanOrEqualTo(prior[i]), "Endpoint " + i + "'s crossing was begun again.");
                    prior[i] = phase;
                }
                Assert.That(Stage<int>(route, "farewellIdleCalls"), Is.EqualTo(2));
                Assert.That(Stage<bool>(route, "farewellIdleAttemptUsed"), Is.True);
                crossingFrames++; yield return null;
            }
            Assert.That(crossingFrames, Is.GreaterThan(0), "MISSING the director's two Idle writes.");
            Assert.That(animators[0].IsInTransition(0) || animators[1].IsInTransition(0), Is.False, "The crossings end in Idle.");
            TestContext.WriteLine("Both endpoints crossed into Idle once, over " + crossingFrames + " frames.");
            yield return episode.ObserveFarewellIdleBeforeCut(route);
            episode.AssertCapturedFarewellBranch(route, false, false); // still one attempt, two writes, at the cut
        }

        // BOTH Idle: nothing is written and no attempt is spent, yet the cut still waits for two NEW
        // evaluated Idle frames after the authorization, each advancing both Idles past their captured phase.
        [UnityTest, Category("FarewellReadinessDecision")]
        public IEnumerator BothIdleEndpointsWriteNothingAndStillWaitForTwoNewEvaluations()
        {
            yield return episode.BeginProvidedFarewellReadiness();
            var route = episode.FarewellReadiness;
            yield return episode.WaitForFarewellBeforeCommit(route);
            var animators = new[] { Force(route.First, IdleState, 0f), Force(route.Second, IdleState, 0f) };
            AssertEndpoint(route.First, true); AssertEndpoint(route.Second, true);
            LogMargin(route, "Both forced into Idle");
            yield return episode.WaitForFarewellAuthorization(route);
            episode.AssertCapturedFarewellBranch(route, true, true); // no attempt, no write
            int authorized = Stage<int>(route, "farewellIdlePreparationFrame");
            var captured = new[] { Stage<object>(route, "farewellIdleA"), Stage<object>(route, "farewellIdleB") };
            Assert.That(Stage<int>(route, "farewellCutFrame"), Is.EqualTo(-1), "Authorization is not an evaluation: no cut on its frame.");
            yield return null;
            for (int i = 0; i < 2; i++)
            {
                Assert.That(animators[i].IsInTransition(0), Is.False, "Endpoint " + i + ": nothing was written, so nothing crosses.");
                Assert.That(animators[i].GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
                Assert.That(animators[i].GetCurrentAnimatorStateInfo(0).normalizedTime,
                    Is.GreaterThan(EpisodePlayModeTests.ReadFarewellField<float>(captured[i], "Phase")),
                    "Endpoint " + i + "'s Idle runs on from its captured phase; it is not restarted.");
            }
            Assert.That(Stage<int>(route, "farewellCutFrame"), Is.EqualTo(-1),
                "No cut on the first evaluation after the authorization (frame " + authorized + "): two new evaluated Idle frames come first.");
            yield return episode.ObserveFarewellIdleBeforeCut(route); // two new evaluations, then the cut
            episode.AssertCapturedFarewellBranch(route, true, true); // still nothing written at the cut
        }

        // A WalkStop whose own exit to Idle is already crossing when the director would authorize: the
        // preparation waits it out without a write, the crossing runs on unrestarted, and the endpoint is
        // captured as the Idle it lands in.
        [UnityTest, Category("FarewellReadinessDecision")]
        public IEnumerator AnExistingStopExitIsWaitedOutAndNeverRestarted()
        {
            yield return episode.BeginProvidedFarewellReadiness();
            var route = episode.FarewellReadiness;
            yield return episode.WaitForFarewellBeforeCommit(route);
            Force(route.Second, IdleState, 0f);
            AssertEndpoint(route.Second, true);
            // FIRST: WalkStop just short of its exit time, then evaluated across it, so the controller's own
            // WalkStop -> Idle exit has begun before the director's next Update can authorize anything.
            var first = Force(route.First, WalkStopState, .5f);
            AssertEndpoint(route.First, false);
            var stop = EpisodePlayModeTests.ReadFarewellSelectedClip(first, "WalkStop");
            first.Update(.1f * stop.length);
            Assert.That(first.IsInTransition(0) && first.GetCurrentAnimatorStateInfo(0).IsName("WalkStop")
                && first.GetNextAnimatorStateInfo(0).IsName("Idle"), Is.True, "MISSING the WalkStop's own exit crossing into Idle.");
            var source = first.GetCurrentAnimatorClipInfo(0);
            var incoming = first.GetNextAnimatorClipInfo(0);
            Assert.That(source.Length == 1 && source[0].clip == stop && incoming.Length == 1
                && incoming[0].clip == EpisodePlayModeTests.ReadFarewellIdleClip(first), Is.True,
                "The crossing runs between the exact WalkStop and Idle clips.");
            LogMargin(route, "First's WalkStop exit crossing");
            float prior = first.GetAnimatorTransitionInfo(0).normalizedTime;
            int crossingFrames = 0, refusedFrames = 0; float by = Time.realtimeSinceStartup + 2f;
            while (first.IsInTransition(0) && Time.realtimeSinceStartup < by)
            {
                episode.AssertNoFarewellPreparation(route); // no attempt, no write, no cut, no take
                Assert.That(Stage<int>(route, "farewellIdlePreparationFrame"), Is.EqualTo(-1), "Nothing is authorized during the crossing.");
                Assert.That(first.GetCurrentAnimatorStateInfo(0).IsName("WalkStop") && first.GetNextAnimatorStateInfo(0).IsName("Idle"), Is.True);
                float phase = first.GetAnimatorTransitionInfo(0).normalizedTime;
                Assert.That(phase, Is.GreaterThanOrEqualTo(prior), "The existing crossing was begun again.");
                prior = phase;
                if (Stage<int>(route, "farewellReadyFrames") >= 2
                    && Stage<string>(route, "farewellLastRefusal") == "first exact Idle/WalkStop preparation") refusedFrames++;
                crossingFrames++; yield return null;
            }
            Assert.That(first.IsInTransition(0), Is.False, "The crossing ends by itself.");
            Assert.That(refusedFrames, Is.GreaterThan(0),
                "MISSING the director's own attempt during the crossing, refused at the first endpoint's capture.");
            TestContext.WriteLine("The exit crossing ran " + crossingFrames + " frames; the director refused on " + refusedFrames + ".");
            yield return episode.WaitForFarewellAuthorization(route);
            LogMargin(route, "Authorized after the crossing");
            episode.AssertCapturedFarewellBranch(route, true, true); // the crossing landed in Idle: nothing written
            yield return episode.ObserveFarewellIdleBeforeCut(route);
        }
    }
}
#endif
