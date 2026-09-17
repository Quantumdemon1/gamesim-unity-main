using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Which body each houseguest is actually wearing, asserted rather than inferred from a
    /// screenshot.
    ///
    /// <para>There are three possible bodies and they look different enough that a person can tell
    /// them apart in a frame, but only if that character happens to be in shot and lit. The cast is
    /// spread across eight rooms, so "are there still procedural characters in the game" is a
    /// question a screenshot answers badly and an enumeration answers exactly.</para>
    ///
    /// <para>The three, in the order <see cref="CharacterPresentation"/> tries them: a provided body
    /// (UMA, when the scene opts in), an authored prefab from Resources, and — only if both fail —
    /// an articulated rig built from Unity primitives. That last one is the fallback this test
    /// exists to catch, because it is the one nobody chooses.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The joint the primitive fallback builds, and nothing else does.</summary>
        private const string FallbackMarker = "Head pivot";

        [UnityTest]
        public IEnumerator Cast_NobodyFallsBackToTheProceduralBody()
        {
            yield return SettleCast();

            var visuals = SceneComponents<CharacterPresentation>();
            Assert.That(visuals, Has.Length.EqualTo(6), "The house should present six houseguests.");

            var procedural = visuals
                .Where(visual => visual.GetComponentsInChildren<Transform>(true)
                    .Any(node => node.name == FallbackMarker))
                .Select(visual => visual.CharacterId)
                .ToArray();

            Assert.That(procedural, Is.Empty,
                "These houseguests are wearing the primitive fallback rather than a real model: "
                + string.Join(", ", procedural)
                + ". That happens when the provided body and the authored prefab both fail to load.");

            // And every one of them is actually wearing something with geometry, so "no fallback"
            // cannot be satisfied by a character wearing nothing at all.
            foreach (var visual in visuals)
            {
                var body = visual.transform.Find("Gamesim Character Visual");
                Assert.That(body, Is.Not.Null, visual.CharacterId + " has no visual root.");

                bool hasMesh = body.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any()
                    || body.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.enabled);
                Assert.That(hasMesh, Is.True, visual.CharacterId + " has a visual root with nothing in it.");
            }

            // Report which body the cast is on, so a run that silently changed casts is visible in
            // the log rather than only in a frame somebody has to open.
            foreach (var visual in visuals)
            {
                var body = visual.transform.Find("Gamesim Character Visual");
                bool uma = body != null && body.GetComponentsInChildren<Transform>(true)
                    .Any(node => node.name == "UMA Body");
                bool skinned = body != null && body.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any();
                Debug.Log("[Gamesim] cast body · " + visual.CharacterId + ": "
                    + (uma ? "UMA" : skinned ? "authored prefab" : "primitive"));
            }
        }

        /// <summary>
        /// No houseguest still shows the U02 capsule underneath whatever replaced it.
        ///
        /// <para>Separate from the fallback check because it is a different failure: the placeholder
        /// renderers are switched off by the presentation, and a character can perfectly well have a
        /// correct body with the old capsule still drawing inside it.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator Cast_NoPlaceholderCapsuleStillRenders()
        {
            yield return SettleCast();

            var showing = SceneComponents<CharacterPresentation>()
                .Select(visual => new
                {
                    visual.CharacterId,
                    Body = visual.transform.Find("Body")?.GetComponent<Renderer>(),
                    Head = visual.transform.Find("Head")?.GetComponent<Renderer>(),
                })
                .Where(entry => (entry.Body != null && entry.Body.enabled)
                    || (entry.Head != null && entry.Head.enabled))
                .Select(entry => entry.CharacterId)
                .ToArray();

            Assert.That(showing, Is.Empty,
                "These houseguests are still drawing their U02 placeholder capsule: "
                + string.Join(", ", showing) + ".");
        }
    }
}
