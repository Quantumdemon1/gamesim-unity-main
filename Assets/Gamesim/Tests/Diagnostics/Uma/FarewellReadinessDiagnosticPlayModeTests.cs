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
    /// authorization. On the UMA cast the evicted always stands into WalkStop and the survivor into Idle,
    /// so none of the three combinations occurs and these cannot pass; they are kept for a gather that
    /// would produce them, never acceptance. Moved here from Gamesim.Tests.PlayMode.EpisodePlayModeTests
    /// (where they compiled behind GAMESIM_DIAGNOSTICS with no floor) by the lead's ruling of 2026-10-16,
    /// unchanged but for reaching the fixture through its shared readiness helper, so this assembly's floor
    /// counts them. The product branches they name are acceptance-tested directly, by forcing each
    /// combination, in Gamesim.Uma.Tests.UmaFarewellReadinessDecisionPlayModeTests.
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

        // Diagnostic only: the UMA cast never stands into both WalkStop, and this refuses to force it.
        [UnityTest, Category("FarewellReadinessDiagnostic")]
        public IEnumerator GroundedFarewell_ReadinessBothWalkStopCallsOnlyOnce()
        {
            yield return episode.BeginProvidedFarewellReadiness();
            var route = episode.FarewellReadiness;
            yield return episode.WaitForFarewellAuthorization(route);
            episode.AssertCapturedFarewellBranch(route, false, false);
            yield return episode.ObserveFarewellIdleBeforeCut(route);
            episode.AssertCapturedFarewellBranch(route, false, false);
        }

        // Diagnostic only: the evicted always stands into WalkStop on the UMA cast, so both Idle never
        // occurs naturally, and this refuses to force it.
        [UnityTest, Category("FarewellReadinessDiagnostic")]
        public IEnumerator GroundedFarewell_ReadinessBothIdleStillRequiresNewEvaluations()
        {
            yield return episode.BeginProvidedFarewellReadiness(.35f);
            var route = episode.FarewellReadiness;
            yield return episode.WaitForFarewellAuthorization(route);
            episode.AssertCapturedFarewellBranch(route, true, true);
            yield return episode.ObserveFarewellIdleBeforeCut(route);
            episode.AssertCapturedFarewellBranch(route, true, true);
        }

        // Diagnostic only: on the UMA cast the authorization comes while the evicted's WalkStop still
        // plays, never after its exit to Idle has begun, so no global crossing exists to be observed
        // first; this refuses to manufacture one.
        [UnityTest, Category("FarewellReadinessDiagnostic")]
        public IEnumerator GroundedFarewell_ReadinessDoesNotRestartAnExistingGlobalStopExit()
        {
            yield return episode.BeginProvidedFarewellReadiness(.1f);
            var route = episode.FarewellReadiness;
            Animator observed = null; float priorPhase = 0f;
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
                        observed = animator; priorPhase = animator.GetAnimatorTransitionInfo(0).normalizedTime; break;
                    }
                }
                if (observed == null) yield return null;
            }
            Assert.That(observed, Is.Not.Null, "MISSING genuine global transition witness; no local transition is manufactured.");
            episode.AssertNoFarewellPreparation(route);
            yield return null;
            if (observed.IsInTransition(0))
                Assert.That(observed.GetAnimatorTransitionInfo(0).normalizedTime, Is.GreaterThanOrEqualTo(priorPhase),
                    "The original crossing is not restarted.");
            yield return episode.WaitForFarewellAuthorization(route);
            yield return episode.ObserveFarewellIdleBeforeCut(route);
        }
    }
}
#endif
