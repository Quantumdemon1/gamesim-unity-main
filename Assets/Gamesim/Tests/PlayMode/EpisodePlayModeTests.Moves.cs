using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The moves the mocap brought (2026-09-27), as a body makes them in the house. Every wait is
    /// bounded in real seconds.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
#if GAMESIM_UMA
        // UMA only: the chair's clap is a mocap take on the humanoid controller, which only a UMA body wears.
        /// <summary>
        /// A sitting body answers a cheer from its seat - the clap captured sitting - where the
        /// standing reactions cannot be played at all, and stands up out of it.
        /// </summary>
        [UnityTest]
        public IEnumerator Moves_ASittingBodyAnswersACheerFromItsSeat()
        {
            var visual = player.GetComponent<CharacterPresentation>();
            yield return WaitFor(() => visual.CanAct(CharacterPresentation.BodyActivity.Posing), 30f,
                "The player's body is built on the controller that claps sitting.");
            // The animator the presentation drives: the one whose controller has a seated clap.
            var animators = player.GetComponentsInChildren<Animator>(true);
            var animator = animators.FirstOrDefault(candidate => candidate.runtimeAnimatorController != null
                && candidate.parameters.Any(parameter => parameter.name == "SeatedClap"));
            Assert.That(animator, Is.Not.Null, "An animator that claps sitting, among: " + string.Join(", ", animators.Select(candidate =>
                candidate.name + (candidate.isActiveAndEnabled ? "" : " (off)") + " " + (candidate.runtimeAnimatorController != null ? candidate.runtimeAnimatorController.name : "no controller"))));
            visual.SetReducedMotion(false);
            Assert.That(visual.SupportsSeated(CharacterPresentation.Reaction.Cheered), Is.True);
            visual.SetSeated(true);
            yield return WaitFor(() => animator.GetCurrentAnimatorStateInfo(0).IsName("SitIdle"), 3f, "The body sits.");
            visual.React(CharacterPresentation.Reaction.Cheered);
            yield return WaitFor(() => animator.GetCurrentAnimatorStateInfo(0).IsName("SitClap"), 3f, "and claps from its seat.");
            Assert.That(visual.LastReaction, Is.EqualTo(CharacterPresentation.Reaction.Cheered));
            visual.SetSeated(false);
            yield return WaitFor(() => animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), 3f, "and stands up out of the clap.");
        }
#endif
    }
}
