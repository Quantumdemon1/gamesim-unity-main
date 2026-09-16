using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The body-provider seam exists so UMA can supply houseguest bodies without the rest of the
    /// project depending on UMA. Two properties make that safe, and both are asserted here rather
    /// than assumed:
    ///
    /// <para>With no provider registered, <see cref="CharacterPresentation"/> behaves exactly as it
    /// always has. Every other PlayMode test in this suite builds its scene in code and registers
    /// nothing, so if this stopped holding, those tests would be silently exercising a different
    /// character system than the one they were written against.</para>
    ///
    /// <para>With a provider registered, it is actually consulted — first, before the authored
    /// prefab — and it is told about palette changes.</para>
    ///
    /// The fake provider here deliberately does not reference UMA. The seam is the contract; UMA is
    /// one implementation of it.
    /// </summary>
    public sealed class CharacterBodyProviderPlayModeTests
    {
        private const string VisualName = "Gamesim Character Visual";

        private GameObject actor;
        private RecordingProvider provider;

        [SetUp]
        public void CreateActor()
        {
            actor = new GameObject("Body provider actor");
            provider = null;
        }

        [UnityTearDown]
        public IEnumerator DestroyActor()
        {
            // Never leave a provider installed; it is process-wide state.
            if (provider != null) CharacterBodySource.Unregister(provider);
            if (actor != null) Object.Destroy(actor);
            yield return null;
            Assert.That(CharacterBodySource.Provider, Is.Null, "A test must not leak a provider.");
        }

        [UnityTest]
        public IEnumerator NoProvider_BuildsTheProjectsOwnBody()
        {
            Assert.That(CharacterBodySource.Provider, Is.Null, "Nothing may register a provider by default.");

            var presentation = CharacterPresentation.Attach(actor, Houseguest(), Color.cyan);
            yield return null;

            Assert.That(presentation, Is.Not.Null);
            AssertBuiltWithoutAProvidedBody();
        }

        [UnityTest]
        public IEnumerator RegisteredProvider_IsAskedForTheBody()
        {
            provider = new RecordingProvider();
            CharacterBodySource.Register(provider);

            var presentation = CharacterPresentation.Attach(actor, Houseguest(), Color.magenta);
            yield return null;

            Assert.That(presentation, Is.Not.Null);
            Assert.That(provider.Requests, Is.EqualTo(1), "The provider is consulted before any fallback.");
            Assert.That(provider.LastAppearanceId, Is.Not.Null.And.Not.Empty,
                "A provider is keyed by persona, never by display name.");
            Assert.That(provider.LastWardrobe, Is.EqualTo(Color.magenta));

            var visual = actor.transform.Find(VisualName);
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.GetComponentsInChildren<Transform>(true).Any(t => t.name == RecordingProvider.BodyName),
                Is.True, "The provided body is what ends up under the visual root.");
            Assert.That(visual.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Head pivot"), Is.False,
                "A provided body replaces the primitive rig rather than stacking on it.");
        }

        [UnityTest]
        public IEnumerator DecliningProvider_FallsBackWithoutComplaint()
        {
            provider = new RecordingProvider { Supply = false };
            CharacterBodySource.Register(provider);

            CharacterPresentation.Attach(actor, Houseguest(), Color.yellow);
            yield return null;

            Assert.That(provider.Requests, Is.EqualTo(1));
            AssertBuiltWithoutAProvidedBody();
        }

        [UnityTest]
        public IEnumerator PaletteChange_ReachesTheProviderInsteadOfTheMaterials()
        {
            provider = new RecordingProvider();
            CharacterBodySource.Register(provider);

            var houseguest = Houseguest();
            CharacterPresentation.Attach(actor, houseguest, Color.magenta);
            yield return null;
            Assert.That(provider.Recolors, Is.EqualTo(0));

            CharacterPresentation.Attach(actor, houseguest, Color.green);
            yield return null;

            Assert.That(provider.Recolors, Is.EqualTo(1), "Only the provider knows how its body is coloured.");
            Assert.That(provider.LastWardrobe, Is.EqualTo(Color.green));
            Assert.That(provider.Requests, Is.EqualTo(1), "A recolour must not rebuild the body.");
        }

        [UnityTest]
        public IEnumerator Unregister_RestoresTheFallbackForLaterCharacters()
        {
            provider = new RecordingProvider();
            CharacterBodySource.Register(provider);
            CharacterPresentation.Attach(actor, Houseguest(), Color.white);
            yield return null;

            CharacterBodySource.Unregister(provider);
            Assert.That(CharacterBodySource.Provider, Is.Null);

            var second = new GameObject("Second actor");
            try
            {
                CharacterPresentation.Attach(second, Houseguest(), Color.white);
                yield return null;
                var visual = second.transform.Find(VisualName);
                Assert.That(visual, Is.Not.Null);
                Assert.That(visual.GetComponentsInChildren<Transform>(true).Any(t => t.name == RecordingProvider.BodyName),
                    Is.False, "Once unregistered, the provider must not be consulted again.");
            }
            finally
            {
                Object.Destroy(second);
            }
        }

        /// <summary>
        /// Asserts the presentation built a body of its own. Deliberately does not name which one:
        /// the fallback is an authored prefab from <c>Resources/GamesimCharacters/</c> when the
        /// persona has one and the primitive rig when it does not, and both are correct answers.
        /// What matters is that no provider supplied it.
        /// </summary>
        private void AssertBuiltWithoutAProvidedBody()
        {
            var visual = actor.transform.Find(VisualName);
            Assert.That(visual, Is.Not.Null, "The named visual root is a test contract.");
            Assert.That(visual.GetComponentsInChildren<Transform>(true).Any(t => t.name == RecordingProvider.BodyName),
                Is.False, "No provided body may appear when no provider supplied one.");
            Assert.That(visual.GetComponentsInChildren<Renderer>(true), Is.Not.Empty,
                "The houseguest still needs a body of some kind.");
        }

        private static ContestantState Houseguest() =>
            ContentCatalog.Create(1).contestants.First(contestant => !contestant.isPlayer);

        /// <summary>A provider that records what it was asked, and hands back a bare marked body.</summary>
        private sealed class RecordingProvider : ICharacterBodyProvider
        {
            internal const string BodyName = "Fake provided body";

            internal bool Supply = true;
            internal int Requests, Recolors;
            internal string LastAppearanceId;
            internal Color LastWardrobe;

            public bool TryCreate(string appearanceId, Transform parent, Color wardrobe, out CharacterBody body)
            {
                Requests++;
                LastAppearanceId = appearanceId;
                LastWardrobe = wardrobe;
                if (!Supply) { body = default; return false; }

                var root = new GameObject(BodyName);
                root.transform.SetParent(parent, false);
                body = new CharacterBody(root, root.AddComponent<Animator>(), deferred: true);
                return true;
            }

            public void SetWardrobeColor(in CharacterBody body, Color wardrobe)
            {
                Recolors++;
                LastWardrobe = wardrobe;
            }
        }
    }
}
