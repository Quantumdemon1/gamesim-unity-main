#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Gamesim.Tests.PlayMode;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Uma.Tests.Diagnostics
{
    /// <summary>
    /// Three farewell readiness tests that wait for an Idle/WalkStop combination to occur naturally and
    /// refuse to force one: both WalkStop, both Idle, and a WalkStop exit already crossing before the
    /// authorization. On the UMA cast the evicted ordinarily stands into WalkStop and the survivor into
    /// Idle, so whether a test's combination occurs depends on how the walk's timing falls: in runs
    /// w1s3b1 and w1s3c1 none occurred, in runs r3s3b and r1 both Idle did (the evicted settled within
    /// BothIdle's .35 s card reading) and the other two did not. They are diagnostics, never acceptance: a
    /// pass is a measurement, and a combination that did not occur is no measurement at all, so it is
    /// reported Inconclusive with the endpoints that did occur (the r1 triage's D2, 2026-10-16), never as
    /// a failure. Once the requested combination does occur, every assertion after it is hard. An
    /// Inconclusive reports failed=0 and exit 2, so read the cases by name. Moved here from
    /// Gamesim.Tests.PlayMode.EpisodePlayModeTests (where they compiled behind GAMESIM_DIAGNOSTICS with no
    /// floor) by the lead's ruling of 2026-10-16, reaching the fixture through its shared readiness helper,
    /// so this assembly's floor counts them. The product branches they name are acceptance-tested
    /// directly, by forcing each combination, in Gamesim.Uma.Tests.UmaFarewellReadinessDecisionPlayModeTests.
    /// </summary>
    public sealed class FarewellReadinessDiagnosticPlayModeTests
    {
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

        // Diagnostic only: the UMA cast's survivor stands into Idle, so both WalkStop has not occurred
        // (Inconclusive, naming the endpoints that did), and this refuses to force it.
        [UnityTest, Category("FarewellReadinessDiagnostic")]
        public IEnumerator GroundedFarewell_ReadinessBothWalkStopCallsOnlyOnce()
        {
            yield return episode.BeginProvidedFarewellReadiness();
            var route = episode.FarewellReadiness;
            yield return episode.WaitForFarewellAuthorization(route);
            RequireTheNaturalCombination(route, false, false);
            episode.AssertCapturedFarewellBranch(route, false, false);
            yield return episode.ObserveFarewellIdleBeforeCut(route);
            episode.AssertCapturedFarewellBranch(route, false, false);
        }

        // Diagnostic only: the evicted ordinarily stands into WalkStop on the UMA cast, so both Idle occurs
        // only when the evicted settles within the card reading (it did in r3s3b and r1, not in w1s3b1 or
        // w1s3c1); otherwise it is Inconclusive, and this refuses to force it.
        [UnityTest, Category("FarewellReadinessDiagnostic")]
        public IEnumerator GroundedFarewell_ReadinessBothIdleStillRequiresNewEvaluations()
        {
            yield return episode.BeginProvidedFarewellReadiness(.35f);
            var route = episode.FarewellReadiness;
            yield return episode.WaitForFarewellAuthorization(route);
            RequireTheNaturalCombination(route, true, true);
            episode.AssertCapturedFarewellBranch(route, true, true);
            yield return episode.ObserveFarewellIdleBeforeCut(route);
            episode.AssertCapturedFarewellBranch(route, true, true);
        }

        // Diagnostic only: on the UMA cast the authorization comes while the evicted's WalkStop still
        // plays, so the WalkStop to Idle crossing is seen only after the director has used its Idle
        // attempt (r3s3b, r1). A crossing counts as the witness only while that attempt is unused; one seen
        // after it, or none at all, is Inconclusive, and this refuses to manufacture one.
        [UnityTest, Category("FarewellReadinessDiagnostic")]
        public IEnumerator GroundedFarewell_ReadinessDoesNotRestartAnExistingGlobalStopExit()
        {
            yield return episode.BeginProvidedFarewellReadiness(.1f);
            var route = episode.FarewellReadiness;
            Animator observed = null; float priorPhase = 0f; bool afterTheAttempt = false;
            float by = Time.realtimeSinceStartup + 2f;
            while (observed == null && Time.realtimeSinceStartup < by && Time.unscaledTime <= route.Deadline)
            {
                foreach (var body in new[] { route.First, route.Second })
                {
                    var animator = EpisodePlayModeTests.ReadFarewellAnimator(body);
                    if (animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).IsName("WalkStop")
                        && animator.GetNextAnimatorStateInfo(0).IsName("Idle"))
                    {
                        var source = animator.GetCurrentAnimatorClipInfo(0);
                        var incoming = animator.GetNextAnimatorClipInfo(0);
                        var basis = Resources.Load<AnimatorOverrideController>("Animation/GamesimHumanoid").runtimeAnimatorController;
                        var stop = basis.animationClips.Single(clip => clip != null && clip.name == "WalkStop");
                        Assert.That(source.Length == 1 && incoming.Length == 1 && source[0].clip == stop
                            && incoming[0].clip == EpisodePlayModeTests.ReadFarewellIdleClip(animator)
                            && source[0].weight >= 0f && source[0].weight <= 1f
                            && incoming[0].weight >= 0f && incoming[0].weight <= 1f, Is.True,
                            "Exact actual global stop/Idle clip objects, not names or a fabricated crossing.");
                        observed = animator; priorPhase = animator.GetAnimatorTransitionInfo(0).normalizedTime;
                        // Read in the frame the crossing is seen: only a crossing before any preparation is the witness.
                        afterTheAttempt = EpisodePlayModeTests.ReadFarewellField<bool>(route.Stage, "farewellIdleAttemptUsed");
                        break;
                    }
                }
                if (observed == null) yield return null;
            }
            if (observed == null)
                Assert.Inconclusive("NOT MEASURED: no WalkStop to Idle crossing occurred within the 2 s window ("
                    + CapturedEndpoints(route) + "). No crossing is manufactured.");
            if (afterTheAttempt)
                Assert.Inconclusive("NOT MEASURED: the crossing came after authorization, once the director had already used its "
                    + "Idle attempt (" + CapturedEndpoints(route) + "); a crossing before any preparation did not occur.");
            episode.AssertNoFarewellPreparation(route);
            yield return null;
            if (observed.IsInTransition(0))
                Assert.That(observed.GetAnimatorTransitionInfo(0).normalizedTime, Is.GreaterThanOrEqualTo(priorPhase),
                    "The original crossing is not restarted.");
            yield return episode.WaitForFarewellAuthorization(route);
            yield return episode.ObserveFarewellIdleBeforeCut(route);
        }

        /// <summary>
        /// Whether the combination a test requests occurred at the authorization, read from the director's
        /// own capture without asserting it (AssertCapturedFarewellBranch, the shipping tests' check, is
        /// unchanged). One that did not is Inconclusive and names the endpoints that did: the evicted
        /// (farewellIdleA) first, the survivor (farewellIdleB) second.
        /// </summary>
        private static void RequireTheNaturalCombination(EpisodePlayModeTests.FarewellReadinessRoute route, bool evictedIdle, bool survivorIdle)
        {
            if (CapturedIdle(route, "farewellIdleA") == evictedIdle && CapturedIdle(route, "farewellIdleB") == survivorIdle) return;
            Assert.Inconclusive("NOT MEASURED: the requested combination, evicted=" + Endpoint(evictedIdle) + " survivor="
                + Endpoint(survivorIdle) + ", did not occur; the endpoints that did: " + CapturedEndpoints(route)
                + ". No state is forced.");
        }

        private static string CapturedEndpoints(EpisodePlayModeTests.FarewellReadinessRoute route)
        {
            if (EpisodePlayModeTests.ReadFarewellField<int>(route.Stage, "farewellIdlePreparationFrame") < 0)
                return "no authorization yet, so no endpoints captured";
            return "evicted=" + Endpoint(CapturedIdle(route, "farewellIdleA"))
                + " survivor=" + Endpoint(CapturedIdle(route, "farewellIdleB"));
        }

        private static bool CapturedIdle(EpisodePlayModeTests.FarewellReadinessRoute route, string field) =>
            EpisodePlayModeTests.ReadFarewellField<bool>(EpisodePlayModeTests.ReadFarewellField(route.Stage, field), "WasIdle");

        private static string Endpoint(bool idle) => idle ? "Idle" : "WalkStop";
    }
}
#endif
