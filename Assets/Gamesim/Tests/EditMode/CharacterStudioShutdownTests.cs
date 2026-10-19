using System;
using System.Collections.Generic;
using System.Reflection;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The studio's real owned resources after Play has ended. Lifecycle methods are invoked
    /// explicitly because an ordinary MonoBehaviour is not an edit-time Update loop.
    /// </summary>
    public sealed class CharacterStudioShutdownTests
    {
        private CharacterStudioPreview preview;
        private RenderTexture ownedTexture, unrelatedTexture;
        private GameObject unrelatedRoot;
        private RecordingProvider provider;
        private ICharacterBodyProvider previousProvider;

        [SetUp]
        public void CreateOwnedAndUnrelatedResources()
        {
            Assert.That(Application.isPlaying, Is.False, "This is the non-playing shutdown branch.");
            previousProvider = CharacterBodySource.Provider;
            provider = new RecordingProvider();
            CharacterBodySource.Register(provider);
            preview = CharacterStudioPreview.Create("Owned shutdown test studio");
            ownedTexture = (RenderTexture)preview.Texture;
            unrelatedRoot = new GameObject("Unrelated shutdown sentinel");
            unrelatedTexture = new RenderTexture(32, 32, 0);
            unrelatedTexture.Create();
        }

        [TearDown]
        public void RemoveOnlyTheFixtureResources()
        {
            try
            {
                if (preview != null) InvokeLifecycle("OnDestroy");
            }
            finally
            {
                // Even an unexpected lifecycle exception must not leave the fixture hierarchy behind.
                if (preview != null) Object.DestroyImmediate(preview.gameObject);
                CharacterBodySource.Unregister(provider);
                // These retained handles also clean a red baseline that leaked its render target.
                if (ownedTexture != null) { ownedTexture.Release(); Object.DestroyImmediate(ownedTexture); }
                if (unrelatedTexture != null) { unrelatedTexture.Release(); Object.DestroyImmediate(unrelatedTexture); }
                if (unrelatedRoot != null) Object.DestroyImmediate(unrelatedRoot);
                Assert.That(CharacterBodySource.Provider, Is.SameAs(previousProvider), "The fixture restores provider ownership.");
            }
        }

        [Test]
        public void ShutdownOutsidePlayReleasesItsTargetDetachesItsCameraAndIsRepeatable()
        {
            var camera = preview.GetComponentInChildren<Camera>();
            Assert.That(ownedTexture.IsCreated(), Is.True);
            Assert.That(camera.targetTexture, Is.SameAs(ownedTexture));

            InvokeLifecycle("OnDestroy");

            Assert.That(ownedTexture == null, Is.True, "Shutdown must destroy the Unity render-target object, not just release its GPU storage.");
            Assert.That(preview.Texture == null, Is.True);
            Assert.That(camera.targetTexture, Is.Null, "No camera may retain the destroyed target.");
            AssertUnrelatedResourcesSurvive();
            InvokeLifecycle("OnDestroy");
            AssertUnrelatedResourcesSurvive();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ReplacingARevisionOutsidePlayRetiresOnlyThePreviousOwnedBody()
        {
            var appearance = CharacterAppearance.Preset("player");
            Build(appearance);
            var oldBody = provider.Roots[0];
            var oldRevision = oldBody.transform.parent.gameObject;
            int firstRevision = oldBody.GetComponent<CharacterBodyBuildState>().Revision;
            var changed = appearance.Clone();
            AppearanceEditing.SetValue(changed, "height", .29f);

            Build(changed);

            Assert.That(provider.Roots.Count, Is.EqualTo(2));
            Assert.That(oldBody == null && oldRevision == null, Is.True,
                "A superseded revision must be gone immediately when no player loop will drain Destroy.");
            Assert.That(provider.Roots[1] != null && provider.Roots[1].activeInHierarchy, Is.True);
            Assert.That(provider.Roots[1].GetComponent<CharacterBodyBuildState>().Revision, Is.GreaterThan(firstRevision));
            Assert.That(provider.Last.Appearance.ContentKey(), Is.EqualTo(changed.ContentKey()));
            Assert.That(preview.Texture, Is.SameAs(ownedTexture));
            Assert.That(ownedTexture.IsCreated(), Is.True, "Replacing a body does not release the studio's target.");
            AssertUnrelatedResourcesSurvive();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DisablingAPendingRevisionOutsidePlayReleasesItsBodyButKeepsItsTargetForReenable()
        {
            var appearance = CharacterAppearance.Preset("player");
            string key = appearance.ContentKey();
            Build(appearance);
            var oldBody = provider.Roots[0];
            var oldRevision = oldBody.transform.parent.gameObject;
            Assert.That(preview.IsBuilding, Is.True);

            InvokeLifecycle("OnDisable");

            Assert.That(oldBody == null && oldRevision == null, Is.True);
            Assert.That(preview.IsBuilding, Is.False);
            Assert.That(preview.Texture, Is.SameAs(ownedTexture));
            Assert.That(ownedTexture.IsCreated(), Is.True);
            InvokeLifecycle("OnDisable");
            AssertUnrelatedResourcesSurvive();

            InvokeLifecycle("OnEnable");
            Assert.That(preview.IsBuilding, Is.True, "Re-enable requests the same pending appearance again.");
            InvokeLifecycle("Build");
            Assert.That(provider.Roots.Count, Is.EqualTo(2));
            Assert.That(provider.Roots[1] != null, Is.True);
            Assert.That(provider.Last.Appearance.ContentKey(), Is.EqualTo(key));
            Assert.That(appearance.ContentKey(), Is.EqualTo(key), "Cleanup cannot change the caller's look.");
            InvokeLifecycle("OnDestroy");
            Assert.That(provider.Roots[1] == null && ownedTexture == null, Is.True);
            AssertUnrelatedResourcesSurvive();
            LogAssert.NoUnexpectedReceived();
        }

        private void Build(CharacterAppearance appearance)
        {
            preview.Show(appearance);
            InvokeLifecycle("Build");
            Assert.That(provider.Last.Purpose, Is.EqualTo(CharacterBuildPurpose.Studio));
        }

        private void InvokeLifecycle(string name)
        {
            var method = typeof(CharacterStudioPreview).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
            Assert.That(method, Is.Not.Null, "Invoke only the existing zero-argument lifecycle method " + name + ".");
            method.Invoke(preview, null);
        }

        private void AssertUnrelatedResourcesSurvive()
        {
            Assert.That(unrelatedRoot != null && unrelatedRoot.activeInHierarchy, Is.True);
            Assert.That(unrelatedTexture != null && unrelatedTexture.IsCreated(), Is.True);
            Assert.That(CharacterBodySource.Provider, Is.SameAs(provider));
        }

        private sealed class RecordingProvider : IModularCharacterBodyProvider
        {
            public readonly List<GameObject> Roots = new List<GameObject>();
            public CharacterBodyRequest Last;
            public ICharacterAppearanceCatalog Catalog => null;

            public bool TryCreate(in CharacterBodyRequest request, Transform parent, Color color, out CharacterBody body)
            {
                Last = request;
                // A deferred owned hierarchy is enough for cleanup; no primitive, mesh or asset is made.
                var root = new GameObject("Owned pending body");
                root.transform.SetParent(parent, false);
                root.AddComponent<CharacterBodyBuildState>().Revision = request.Revision;
                Roots.Add(root);
                body = new CharacterBody(root, null, deferred: true);
                return true;
            }

            public bool TryCreate(string id, Transform parent, Color color, out CharacterBody body)
            {
                body = default;
                throw new InvalidOperationException("The studio must use its explicit appearance request.");
            }

            public void SetWardrobeColor(in CharacterBody body, Color color) { }
        }
    }
}
