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
    /// is a new default one. RS4 pins it where the body is completed (UmaBodyProvider, at creation and
    /// at the end of every build); the first test asserts it of the bodies the provider builds, and that
    /// there are bodies to assert it of, the second of bodies rebuilt and given a new race.
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

        /// <summary>
        /// A rebuilt body keeps AlwaysAnimate (§3.2 e, R15; RS4): the provider pins it where the body
        /// completes, at the end of every build, so a body the room scope hides keeps its bones live
        /// whatever its Animator went through. One body is rebuilt with its Animator set to cull by
        /// hand first (a rebuild keeps the Animator, so only the pin at the build's end puts it back);
        /// the other changes race with UMA initialising the Animator it recreates (UMA's own default
        /// for that is CullCompletely). Read after the builds, on the Animator UMA left each body with.
        /// </summary>
        [UnityTest, Timeout(420000)]
        public IEnumerator Bodies_ARebuiltBodyKeepsAlwaysAnimate()
        {
            cast = new GameObject("Gamesim UMA cast", typeof(GamesimUmaCast));
            yield return null;
            Assert.That(CharacterBodySource.Provider, Is.TypeOf<UmaBodyProvider>(), "The bodies are UMA's.");
            var house = ContentCatalog.Create(1).contestants.Take(2).ToArray();
            Assert.That(house, Has.Length.EqualTo(2));
            for (int index = 0; index < house.Length; index++)
            {
                var actor = new GameObject("Room scope rebuilt houseguest " + index);
                actor.transform.position = new Vector3(index * 1.5f, 0f, 0f);
                spawned.Add(actor);
                CharacterPresentation.Attach(actor, house[index], Color.HSVToRGB(index / 2f, .6f, .9f));
            }
            float deadline = Time.realtimeSinceStartup + BuildTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline && !spawned.All(Built)) yield return null;
            Assert.That(spawned.Count(Built), Is.EqualTo(2), "Both bodies finished building.");
            yield return null;
            var avatars = spawned.Select(actor => actor.GetComponentInChildren<DynamicCharacterAvatar>(true)).ToArray();
            Assert.That(avatars.All(avatar => avatar != null), Is.True, "Every houseguest is an UMA body.");
            var builds = new int[avatars.Length];
            for (int i = 0; i < avatars.Length; i++)
            {
                int which = i;
                avatars[i].OnCharacterUpdated += data => builds[which]++;
            }

            // A rebuild keeps the Animator it has: one set to cull by hand stays culling unless the
            // build's end pins it again.
            var kept = avatars[0].GetComponent<Animator>();
            Assert.That(kept != null, Is.True, "The first body has its Animator.");
            kept.cullingMode = AnimatorCullingMode.CullCompletely;
            avatars[0].umaData.Dirty(true, true, true);

            // A race change recreates the Animator, initialised by UMA as it adds it.
            var replaced = avatars[1].GetComponent<Animator>();
            string race = avatars[1].activeRace.name;
            string other = race == UmaCastLibrary.MaleRace ? UmaCastLibrary.FemaleRace : UmaCastLibrary.MaleRace;
            avatars[1].initializeAnimatorWhenAdded = true;
            avatars[1].RecreateAnimatorOnRaceChange = true;
            avatars[1].ChangeRace(other, true);

            deadline = Time.realtimeSinceStartup + BuildTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline && builds.Any(count => count == 0)) yield return null;
            Assert.That(builds, Has.All.GreaterThan(0), "Both bodies were built again.");
            // A frame more, so the Animator UMA recreated during the build is the one read.
            yield return null;
            Assert.That(avatars[1].activeRace.name, Is.EqualTo(other), "The second body changed race.");
            var after = spawned.Select(actor => actor.GetComponentsInChildren<Animator>(true)).ToArray();
            Assert.That(after.Sum(rigs => rigs.Length), Is.EqualTo(avatars.Length), "One Animator a body, and more than none.");
            Assert.That(after[1].Length == 1 && !ReferenceEquals(after[1][0], replaced), Is.True,
                "UMA recreated the second body's Animator on the race change, so the pin is read on the new one.");
            foreach (var rigs in after)
                foreach (var animator in rigs)
                    Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate),
                        animator.transform.root.name + "'s rebuilt Animator culls when unseen: a hidden body's bones would go stale.");
        }

        private static bool Built(GameObject actor)
        {
            if (actor == null) return false;
            var body = actor.GetComponent<CharacterPresentation>();
            return body != null && !body.IsBodyAssembling && actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any();
        }
    }
}
