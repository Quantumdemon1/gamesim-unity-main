using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Timer = System.Diagnostics.Stopwatch;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator OpeningStage_TheFrontDoorRevealKeepsBodiesClearAtSlowFrames()
        {
            // The original case supplies the continuous actual-leaf/hips watcher, complete door
            // reveal and arrival assertions, including its existing captures and deadlines.
            yield return ObserveWithSlowFrames(OpeningStage_TheFrontDoorRevealWalksAHouseguestIn(),
                () => director.IsOpeningStaged && AllStageBodiesReady());
        }

        [UnityTest]
        public IEnumerator StagedExit_TheSeatedResultAndGoodbyeKeepTheirLifecycleAtSlowFrames()
        {
            // Reuse every seated reaction, card-held restoration, goodbye, pace and release
            // assertion. The result/ceremony clocks and their original time bounds are unchanged.
            yield return ObserveWithSlowFrames(StagedExit_TheGoodbyeComesBetweenTheCardAndTheWalk(),
                () => director.IsCeremonyStaged
                    && director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing
                    && AllStageBodiesReady());
        }

        private bool AllStageBodiesReady()
        {
            var bodies = SceneComponents<CharacterPresentation>().Where(body => body.gameObject.activeInHierarchy).ToArray();
            return bodies.Length > 0 && bodies.All(body => !body.IsBodyAssembling && !body.IsChangingOutfit
                && (CharacterBodySource.Provider == null
                    || body.GetComponentsInChildren<Animator>().Any(animator => animator.isHuman && animator.isActiveAndEnabled)));
        }

        private IEnumerator ObserveWithSlowFrames(IEnumerator original, Func<bool> bodiesReady)
        {
            StageFrameDelay delay = null;
            try
            {
                while (original.MoveNext())
                {
                    if (delay == null && bodiesReady())
                    {
                        delay = new GameObject("Bounded stage frame delay").AddComponent<StageFrameDelay>();
                        delay.Begin();
                    }
                    yield return original.Current;
                }
                Assert.That(delay, Is.Not.Null, "The stage and every provided body must be ready before imposing slow frames.");
                delay.StopAndAssertObserved();
            }
            finally
            {
                (original as IDisposable)?.Dispose();
                if (delay != null) { delay.LogObserved(); delay.enabled = false; UnityEngine.Object.Destroy(delay.gameObject); }
            }
        }

        /// <summary>
        /// A test-only load, bounded by wall clock and installed after character assembly. Sleeping
        /// the main thread exercises the next real frame's ordinary update/cue clocks; it changes
        /// no time scale, capture delta, physics setting, runtime timeout or animation parameter.
        /// </summary>
        [DefaultExecutionOrder(-32000)]
        private sealed class StageFrameDelay : MonoBehaviour
        {
            private const int DelayMilliseconds = 100;
            private const double MaximumSeconds = 120;
            private readonly List<double> intervals = new List<double>();
            private double deadline, previous;
            private bool expired;
            private bool reported;

            public void Begin() { deadline = Seconds() + MaximumSeconds; }
            private static double Seconds() => (double)Timer.GetTimestamp() / Timer.Frequency;

            private void Update()
            {
                double now = Seconds();
                if (now >= deadline) { expired = true; enabled = false; return; }
                if (previous > 0) intervals.Add(now - previous);
                previous = now;
                System.Threading.Thread.Sleep(DelayMilliseconds);
            }

            public void StopAndAssertObserved()
            {
                enabled = false;
                Assert.That(expired, Is.False, "The bounded load must remain active through the whole observed stage lifecycle.");
                Assert.That(intervals.Count, Is.GreaterThanOrEqualTo(10), "At least ten real slow frames must exercise the stage.");
                LogObserved();
                Assert.That(intervals.Min(), Is.GreaterThanOrEqualTo(.09),
                    "Measured frame spacing must confirm the imposed approximately ten-FPS load.");
            }

            public void LogObserved()
            {
                if (reported) return;
                reported = true;
                Debug.Log("[Gamesim] Stage slow-frame spacing: count=" + intervals.Count
                    + (intervals.Count == 0 ? "; no interval observed" :
                    ", min=" + intervals.Min().ToString("F3") + "s, mean=" + intervals.Average().ToString("F3")
                    + "s, max=" + intervals.Max().ToString("F3") + "s") + "; intended delay=" + DelayMilliseconds + "ms.");
            }
        }
    }
}
