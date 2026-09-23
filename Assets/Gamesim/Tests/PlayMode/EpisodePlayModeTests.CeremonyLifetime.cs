using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// How long a ceremony card stays up, and what ends it.
    ///
    /// <para>The key ceremony printed "Click anywhere to continue" and read no clicks; nothing took
    /// it down when the next beat started, so the nomination's keys - sorted over the takeover -
    /// covered the veto draw that followed; and the camera held its ceremony shot for every card
    /// except that one, so it pulled back to the house halfway through the keys. The walkthrough
    /// captures showed the first two for weeks: every veto frame was a finished nomination card.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>Commands until a nomination ceremony is on screen; fails if none comes.</summary>
        private IEnumerator PlayUntilTheKeyCeremony(KeyCeremony keys)
        {
            for (int guard = 0; guard < 400 && !keys.IsPlaying; guard++)
            {
                var before = director.Snapshot;
                if (before.phase == EpisodePhase.Finished) break;
                director.Submit(NextCommand(before));
                yield return null;
            }
            Assert.That(keys.IsPlaying, Is.True, "The episode never reached a nomination ceremony.");
        }

        [UnityTest]
        public IEnumerator Ceremonies_TheKeyCeremonyAnswersTheClickItAsksFor()
        {
            var keys = SceneComponents<KeyCeremony>().Single();
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return PlayUntilTheKeyCeremony(keys);
                Assert.That(keys.ShowingBlock, Is.False, "The keys should still be coming out.");

                // Past the read-first delay, and still well inside the key-by-key stage.
                float readable = Time.unscaledTime + 0.8f;
                while (Time.unscaledTime < readable) yield return null;
                Assert.That(keys.ShowingBlock, Is.False, "The keys should still be coming out.");

                yield return Click(mouse);
                Assert.That(keys.IsPlaying, Is.True, "The first click shows the block; it does not skip it.");
                Assert.That(keys.ShowingBlock, Is.True, "The first click hands out the remaining keys.");

                yield return Click(mouse);
                Assert.That(keys.IsPlaying, Is.False, "A click on the block ends the card.");
            }
            finally
            {
                if (mouse.added) InputSystem.RemoveDevice(mouse);
            }
        }

        private static IEnumerator Click(Mouse mouse)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator Ceremonies_TheNextBeatTakesDownTheCardStillUp()
        {
            var keys = SceneComponents<KeyCeremony>().Single();
            var takeover = SceneComponents<CeremonyTakeover>().Single();
            yield return PlayUntilTheKeyCeremony(keys);

            // Commit the next beat at once, the way a player who does not wait for the keys does.
            // Whatever card it brings up, the nomination's must be gone from over it.
            for (int guard = 0; guard < 40 && !takeover.IsPlaying; guard++)
            {
                var before = director.Snapshot;
                director.Submit(NextCommand(before));
                yield return null;
            }
            Assert.That(takeover.IsPlaying, Is.True, "The veto draw should bring up its card.");
            Assert.That(keys.IsPlaying, Is.False,
                "The nomination's keys are still up over the veto draw; they sort over it and hide it.");
        }

        [UnityTest]
        public IEnumerator Ceremonies_TheCameraHoldsTheRoomForTheWholeKeyCeremony()
        {
            var keys = SceneComponents<KeyCeremony>().Single();
            yield return PlayUntilTheKeyCeremony(keys);
            Assert.That(director.IsFramingCeremony, Is.True, "The nomination should frame its room.");
            Assert.That(keys.Duration, Is.GreaterThan(EpisodeDirector.CeremonyHoldSeconds + 0.5f),
                "This fixture needs a key ceremony that outlasts the framing's minimum hold.");

            // Past the minimum hold, while the keys are still coming out.
            float past = Time.unscaledTime + EpisodeDirector.CeremonyHoldSeconds + 0.3f;
            while (Time.unscaledTime < past && keys.IsPlaying) yield return null;
            Assert.That(keys.IsPlaying, Is.True, "The key ceremony ended before the hold did.");
            Assert.That(director.IsFramingCeremony, Is.True,
                "The camera left the nomination room while its ceremony was still on screen.");
        }
    }
}
