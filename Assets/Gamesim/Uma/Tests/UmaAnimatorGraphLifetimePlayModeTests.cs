using System.Collections;
using System.Globalization;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UObject = UnityEngine.Object;

namespace Gamesim.Uma.Tests
{
    // A dedicated, one-leaf body-factory lifecycle regression, not a global graph observer.
    [TestFixture]
    public sealed class UmaAnimatorGraphLifetimePlayModeTests
    {
        private const float ReadySeconds = 45f, DestroySeconds = 10f;
        private GameObject ownedCast, actor;
        private ICharacterBodyProvider ownedProvider;
        private string contestantId;
        private Witness first, replacement;
        private int records;

        private sealed class Witness
        {
            public CharacterBody Body;
            public Animator Returned, Built;
            public string ReturnedId, BuiltId, AvatarId;
            public PlayableGraph ReturnedGraph, BuiltGraph;
            public bool ReturnedGraphInitiallyValid, RecreateAnimatorAtReturn;
            public RuntimeAnimatorController Controller;
        }

        [SetUp]
        public void CreateOnlyOwnedFixture()
        {
            Assert.That(CharacterBodySource.Provider == null, Is.True,
                "An existing provider is not this fixture's property and must not be replaced.");
            contestantId = ContentCatalog.Create(1).contestants.First(person => !person.isPlayer).id;
            ownedCast = new GameObject("Graph lifetime owned cast", typeof(GamesimUmaCast));
            ownedProvider = CharacterBodySource.Provider;
            Assert.That(ownedProvider is UmaBodyProvider, Is.True,
                "The installed shipping UMA provider and generator must be available; no fallback or skip.");
            actor = new GameObject("Graph lifetime owned actor");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator ShippingReplacementAndTeardownRetireOnlyFixtureOwnedAnimatorGraphs()
        {
            first = CreateBody();
            Record("initial-returned", first);
            yield return Ready(first);
            CaptureBuilt(first, "initial-built");

            replacement = CreateBody();
            Record("replacement-returned", replacement);
            yield return Ready(replacement);
            CaptureBuilt(replacement, "replacement-built");
            Assert.That(first.Body.Root != replacement.Body.Root && first.Built != replacement.Built
                && !first.BuiltGraph.Equals(replacement.BuiltGraph), Is.True,
                "The replacement must be a distinct owned body, Animator and graph, not the same witness twice.");
            Assert.That(object.ReferenceEquals(first.Controller, replacement.Controller), Is.True,
                "Both real bodies must borrow the same shipping provider controller.");

            GameObject oldRoot = first.Body.Root;
            UObject.Destroy(oldRoot);
            yield return Destroyed(oldRoot);
            Record("first-root-destroyed", first);
            AssertRetired(first);
            Assert.That(replacement.Built != null && replacement.BuiltGraph.IsValid(), Is.True,
                "Retiring one owned body must not destroy the surviving borrower's graph.");
            Assert.That(object.ReferenceEquals(replacement.Built.runtimeAnimatorController, replacement.Controller), Is.True,
                "Do not replace, dispose or clear the surviving body's controller.");
            Record("replacement-survives", replacement);

            GameObject oldActor = actor;
            UObject.Destroy(oldActor);
            yield return Destroyed(oldActor);
            Record("actor-destroyed-first", first);
            Record("actor-destroyed-replacement", replacement);
            AssertRetired(first);
            AssertRetired(replacement);
        }

        [UnityTearDown]
        public IEnumerator DestroyOnlyOwnedFixture()
        {
            if (actor != null)
            {
                GameObject oldActor = actor;
                UObject.Destroy(oldActor);
                yield return Destroyed(oldActor);
            }
            if (ownedCast != null)
            {
                GameObject oldCast = ownedCast;
                UObject.Destroy(oldCast);
                yield return Destroyed(oldCast);
            }
            Record("teardown-first", first);
            Record("teardown-replacement", replacement);
            AssertRetired(first);
            AssertRetired(replacement);
            Assert.That(CharacterBodySource.Provider == null, Is.True,
                "The owned cast unregisters only its provider. A foreign provider is never cleared here.");
        }

        private Witness CreateBody()
        {
            Assert.That(object.ReferenceEquals(CharacterBodySource.Provider, ownedProvider), Is.True,
                "Provider ownership changed; do not overwrite or borrow an unknown registration.");
            var request = new CharacterBodyRequest(contestantId, contestantId, null);
            Assert.That(CharacterBodySource.TryCreate(request, actor.transform, Color.white, out var body), Is.True);
            Assert.That(body.Exists && body.Deferred && object.ReferenceEquals(body.Owner, ownedProvider), Is.True);
            Assert.That(body.Root.transform.parent == actor.transform && body.Animator != null, Is.True);
            var avatar = body.Root.GetComponent<UMA.CharacterSystem.DynamicCharacterAvatar>();
            Assert.That(avatar != null, Is.True, "The real shipping DynamicCharacterAvatar must own this body.");
            var graph = body.Animator.playableGraph;
            return new Witness { Body = body, Returned = body.Animator, ReturnedId = Id(body.Animator),
                ReturnedGraph = graph, ReturnedGraphInitiallyValid = graph.IsValid(),
                AvatarId = Id(avatar), RecreateAnimatorAtReturn = avatar.RecreateAnimatorOnRaceChange };
        }

        private static IEnumerator Ready(Witness witness)
        {
            float deadline = Time.realtimeSinceStartup + ReadySeconds;
            while (witness.Body.Root != null && Time.realtimeSinceStartup < deadline)
            {
                var state = witness.Body.Root.GetComponent<CharacterBodyBuildState>();
                var mesh = witness.Body.Root.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (state != null && state.Ready && mesh != null && mesh.sharedMesh != null) yield break;
                yield return null;
            }
            Assert.Fail("The owned shipping body did not reach its existing post-build Ready condition.");
        }

        private void CaptureBuilt(Witness witness, string stage)
        {
            witness.Built = witness.Body.Root.GetComponent<Animator>();
            if (witness.Built != null)
            {
                witness.BuiltId = Id(witness.Built);
                witness.BuiltGraph = witness.Built.playableGraph;
                witness.Controller = witness.Built.runtimeAnimatorController;
            }
            Record(stage, witness); // Retain the actual pre-assert observation even on a baseline failure.
            Assert.That(witness.Built != null && witness.Built.isHuman && witness.Built.avatar != null
                && witness.Built.avatar.isValid && witness.Built.runtimeAnimatorController != null, Is.True,
                "Ready must expose the actual live Humanoid Animator, not a delayed or missing graph.");
            Assert.That(witness.BuiltGraph.IsValid(), Is.True, "The post-build graph is a required live witness.");
            bool sameAnimator = witness.Returned == witness.Built;
            Assert.That(sameAnimator, Is.EqualTo(witness.ReturnedId == witness.BuiltId));
            if (!sameAnimator)
                Assert.That(witness.Returned == null, Is.True, "A replacement must retire its earlier Animator.");
            if (witness.ReturnedGraphInitiallyValid && !witness.ReturnedGraph.Equals(witness.BuiltGraph))
                Assert.That(witness.ReturnedGraph.IsValid(), Is.False, "An earlier valid graph outlived replacement.");
        }

        private static IEnumerator Destroyed(GameObject owned)
        {
            float deadline = Time.realtimeSinceStartup + DestroySeconds;
            while (owned != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(owned == null, Is.True, "Owned destruction failed; no safe-cleanup claim or extra delay.");
        }

        private static void AssertRetired(Witness witness)
        {
            if (witness == null) return; // Setup failure has no invented body/graph witness.
            Assert.That(witness.Returned == null && witness.Built == null, Is.True,
                "A destroyed owned body still has a live captured Animator.");
            Assert.That(witness.ReturnedGraph.IsValid() || witness.BuiltGraph.IsValid(), Is.False,
                "A captured owned graph remains valid after actual body destruction.");
        }

        private void Record(string stage, Witness witness)
        {
            if (witness == null) return;
            Assert.That(++records, Is.LessThanOrEqualTo(12));
            string row = "[Gamesim.Uma.GraphLifetime] stage=" + stage
                + " frame=" + Time.frameCount.ToString(CultureInfo.InvariantCulture) + " returned=" + witness.ReturnedId
                + " built=" + (witness.BuiltId ?? "uncaptured")
                + " avatar=" + witness.AvatarId + " recreateAtReturn=" + witness.RecreateAnimatorAtReturn
                + " returnedAlive=" + (witness.Returned != null) + " builtAlive=" + (witness.Built != null)
                + " initialGraphValid=" + witness.ReturnedGraphInitiallyValid
                + " returnedGraphToken=" + witness.ReturnedGraph.GetHashCode().ToString(CultureInfo.InvariantCulture)
                + " returnedGraphValid=" + witness.ReturnedGraph.IsValid()
                + " builtGraphToken=" + witness.BuiltGraph.GetHashCode().ToString(CultureInfo.InvariantCulture)
                + " builtGraphValid=" + witness.BuiltGraph.IsValid();
            Assert.That(row.Length, Is.LessThanOrEqualTo(800), "Bounded identity-only lifecycle receipt required.");
            Debug.Log(row);
        }

        private static string Id(UObject value)
        {
            string id = value.GetEntityId().ToString();
            Assert.That(id.Length <= 64 && id.All(character => character >= ' ' && character <= '~'), Is.True,
                "Only bounded public fixture-object identity text may be recorded.");
            return id;
        }
    }
}
