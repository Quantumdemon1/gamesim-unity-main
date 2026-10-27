using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UMA.CharacterSystem;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Uma.Tests
{
    /// <summary>
    /// The room scope's body pin (ROOM-SCOPE-PLAN §5.2 RS0, D9): every UMA body animates whether or not
    /// a camera sees it, because hidden bodies must keep their bones live - witness sightlines, seat
    /// fitting and framing read them. Today that holds by default: the provider adds the Animator
    /// without setting a culling mode (UmaBodyProvider), UMA's own CullCompletely applies only with
    /// <c>initializeAnimatorWhenAdded</c>, which is off, and the Animator UMA recreates on a race change
    /// is a new default one. RS4 pins it where the body is completed; this asserts it of the bodies the
    /// provider builds now, and that there are bodies to assert it of.
    /// </summary>
    public sealed class UmaRoomScopeBodiesPlayModeTests
    {
        private const int HouseSize = 3;
        private const float BuildTimeoutSeconds = 180f;

        private GameObject cast;
        private readonly List<GameObject> spawned = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var actor in spawned) if (actor != null) Object.Destroy(actor);
            spawned.Clear();
            if (cast != null) Object.Destroy(cast);
            yield return null;
            Assert.That(CharacterBodySource.Provider, Is.Null, "The cast component must unregister itself.");
        }

        // Past the build's own bound, so a slow cold build fails on "Every body finished building", not the framework's 180 s default.
        [UnityTest, Timeout(240000)]
        public IEnumerator Bodies_EveryAnimatorAlwaysAnimates()
        {
            cast = new GameObject("Gamesim UMA cast", typeof(GamesimUmaCast));
            yield return null;
            Assert.That(CharacterBodySource.Provider, Is.TypeOf<UmaBodyProvider>(), "The bodies are UMA's.");
            var house = ContentCatalog.Create(1).contestants.Take(HouseSize).ToArray();
            Assert.That(house, Has.Length.EqualTo(HouseSize));
            int index = 0;
            foreach (var contestant in house)
            {
                var actor = new GameObject("Room scope houseguest " + index);
                actor.transform.position = new Vector3(index * 1.5f, 0f, 0f);
                spawned.Add(actor);
                CharacterPresentation.Attach(actor, contestant, Color.HSVToRGB(index / (float)HouseSize, .6f, .9f));
                index++;
            }
            // UMA builds over frames; bounded on the real clock, never a frame count.
            float deadline = Time.realtimeSinceStartup + BuildTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline && !spawned.All(Built)) yield return null;
            Assert.That(spawned.Count(Built), Is.EqualTo(HouseSize), "Every body finished building.");
            // A frame more, so an Animator UMA recreated during the build is the one read.
            yield return null;

            var bodies = spawned.Select(actor => actor.GetComponentInChildren<DynamicCharacterAvatar>(true)).ToArray();
            Assert.That(bodies, Has.All.Not.Null, "Every houseguest is an UMA body.");
            var animators = spawned.SelectMany(actor => actor.GetComponentsInChildren<Animator>(true)).ToArray();
            Assert.That(animators.Length, Is.GreaterThan(0), "Bodies with Animators to read: the primitive rig has none, and would pass this on nothing.");
            Assert.That(animators.Length, Is.EqualTo(bodies.Length), "One Animator a body.");
            foreach (var animator in animators)
                Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate),
                    animator.transform.root.name + "'s Animator culls when unseen: a hidden body's bones would go stale.");
        }

        private static bool Built(GameObject actor)
        {
            if (actor == null) return false;
            var body = actor.GetComponent<CharacterPresentation>();
            return body != null && !body.IsBodyAssembling && actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any();
        }
    }
}
