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

        [Test]
        public void PortraitQueueShutdownOutsidePlayRetiresOnlyItsOwnStudioCameraAndTarget()
        {
            GameObject queueRoot = null;
            CharacterStudioPreview portrait = null;
            RenderTexture portraitTarget = null;
            try
            {
                queueRoot = new GameObject("Owned portrait shutdown queue");
                var queue = queueRoot.AddComponent<CharacterPortraitQueue>();
                var appearance = CharacterAppearance.Preset("player");
                string key = appearance.ContentKey();
                StartQueuedPortrait(queue, "owned-portrait-shutdown", appearance, out portrait);
                var studioRoot = portrait.gameObject;
                var target = (RenderTexture)portrait.Texture;
                portraitTarget = target;
                var camera = portrait.GetComponentInChildren<Camera>();
                Assert.That(studioRoot != null && target != null && target.IsCreated() && camera != null, Is.True);
                Assert.That(camera.targetTexture, Is.SameAs(target));
                Assert.That(provider.Roots, Is.Empty, "Enqueue/queue Update requests a studio; this test does not force a body build.");

                // Invoke the existing production cleanup method explicitly; EditMode dispatcher behavior is not assumed.
                Assert.That(!Application.isPlaying && portrait.enabled && studioRoot.activeInHierarchy
                    && (studioRoot.hideFlags & HideFlags.DontSave) == HideFlags.DontSave, Is.True,
                    "The exact queue-created studio must be an active transient EditMode component before callback opt-in.");
                bool foreignEditMode = preview.runInEditMode;
                portrait.runInEditMode = true;
                Assert.That(portrait.runInEditMode, Is.True, "Only this exact owned studio opts in to Editor callbacks.");
                var queueOwner = typeof(CharacterPortraitQueue).GetField("preview", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(queueOwner != null && object.ReferenceEquals(queueOwner.GetValue(queue), portrait), Is.True,
                    "Activation must not replace the real queue owner.");
                Assert.That(portrait != null && portrait.enabled && portrait.gameObject == studioRoot && studioRoot.activeInHierarchy
                    && (studioRoot.hideFlags & HideFlags.DontSave) == HideFlags.DontSave
                    && portrait.Texture == target && target != null && target.IsCreated()
                    && camera != null && portrait.GetComponentInChildren<Camera>() == camera && camera.targetTexture == target, Is.True,
                    "Opt-in must not pre-clean or replace the exact studio/camera/native target before the owner cleanup.");
                Assert.That(provider.Roots.Count == 0 && portrait.IsBuilding, Is.True,
                    "The original queued request remains pending and activation must not create any extra provider body.");
                Assert.That(preview != null && preview.runInEditMode == foreignEditMode
                    && ownedTexture != null && ownedTexture.IsCreated() && preview.Texture == ownedTexture, Is.True,
                    "The separate studio and its target/Editor mode are untouched.");
                AssertUnrelatedResourcesSurvive();
                InvokeQueueShutdown(queue);
                TestContext.WriteLine("GAMESIM_PORTRAIT_CLEANUP_OPTIN02 plain queueRootGone=" + (queueRoot == null)
                    + " studioRootGone=" + (studioRoot == null) + " studioGone=" + (portrait == null)
                    + " cameraGone=" + (camera == null) + " targetGone=" + (target == null));

                Assert.That(studioRoot == null && portrait == null && camera == null && target == null, Is.True,
                    "The queue must immediately retire the exact studio/camera/target that it created when no Play loop can drain Destroy.");
                Assert.That(appearance.ContentKey(), Is.EqualTo(key));
                Assert.That(preview != null && ownedTexture != null && ownedTexture.IsCreated(), Is.True,
                    "A different studio and its target are not owned by this queue.");
                Assert.That(preview.GetComponentInChildren<Camera>().targetTexture, Is.SameAs(ownedTexture));
                AssertUnrelatedResourcesSurvive();
                InvokeQueueShutdown(queue);
                AssertUnrelatedResourcesSurvive();
                Object.DestroyImmediate(queueRoot);
                LogAssert.NoUnexpectedReceived();
            }
            finally { DestroyPortraitFixture(queueRoot, portrait, portraitTarget); }
        }

        [Test]
        public void PortraitQueueShutdownOutsidePlayRetiresItsActualPendingRevision()
        {
            GameObject queueRoot = null;
            CharacterStudioPreview portrait = null;
            RenderTexture portraitTarget = null;
            try
            {
                queueRoot = new GameObject("Owned pending portrait shutdown queue");
                var queue = queueRoot.AddComponent<CharacterPortraitQueue>();
                var appearance = CharacterAppearance.Preset("player");
                string key = appearance.ContentKey();
                StartQueuedPortrait(queue, "owned-pending-portrait-shutdown", appearance, out portrait);
                var target = (RenderTexture)portrait.Texture;
                portraitTarget = target;
                var build = typeof(CharacterStudioPreview).GetMethod("Build", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
                Assert.That(build, Is.Not.Null, "Use the existing real studio build entry point.");
                build.Invoke(portrait, null);
                Assert.That(provider.Roots.Count, Is.EqualTo(1));
                Assert.That(provider.Last.Purpose, Is.EqualTo(CharacterBuildPurpose.Studio));
                var body = provider.Roots[0];
                var revision = body.transform.parent.gameObject;
                Assert.That(portrait.IsBuilding && body != null && revision != null, Is.True);
                Assert.That(revision.transform.parent, Is.SameAs(portrait.transform));

                var studioRoot = portrait.gameObject;
                var camera = portrait.GetComponentInChildren<Camera>();
                var builtRevision = provider.Last.Revision;
                Assert.That(!Application.isPlaying && portrait.enabled && studioRoot.activeInHierarchy
                    && (studioRoot.hideFlags & HideFlags.DontSave) == HideFlags.DontSave
                    && target != null && target.IsCreated() && camera != null && camera.targetTexture == target, Is.True,
                    "The exact built studio and its target must exist in active transient EditMode before callback opt-in.");
                bool foreignEditMode = preview.runInEditMode;
                portrait.runInEditMode = true;
                Assert.That(portrait.runInEditMode, Is.True, "Only this exact owned studio opts in to Editor callbacks.");
                var queueOwner = typeof(CharacterPortraitQueue).GetField("preview", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(queueOwner != null && object.ReferenceEquals(queueOwner.GetValue(queue), portrait), Is.True,
                    "Activation must not replace the real queue owner.");
                Assert.That(portrait != null && portrait.enabled && portrait.gameObject == studioRoot && studioRoot.activeInHierarchy
                    && (studioRoot.hideFlags & HideFlags.DontSave) == HideFlags.DontSave
                    && portrait.Texture == target && target != null && target.IsCreated()
                    && camera != null && portrait.GetComponentInChildren<Camera>() == camera && camera.targetTexture == target, Is.True,
                    "Opt-in must not pre-clean or replace the exact studio/camera/native target before the owner cleanup.");
                Assert.That(provider.Roots.Count == 1 && provider.Roots[0] == body && provider.Last.Revision == builtRevision
                    && provider.Last.Purpose == CharacterBuildPurpose.Studio && portrait.IsBuilding
                    && body != null && revision != null && body.transform.parent.gameObject == revision
                    && revision.transform.parent == portrait.transform, Is.True,
                    "The exact original pending body/revision/provider request must survive activation without extra builds or pre-cleaning.");
                Assert.That(preview != null && preview.runInEditMode == foreignEditMode
                    && ownedTexture != null && ownedTexture.IsCreated() && preview.Texture == ownedTexture, Is.True,
                    "The separate studio and its target/Editor mode are untouched.");
                AssertUnrelatedResourcesSurvive();
                InvokeQueueShutdown(queue);
                TestContext.WriteLine("GAMESIM_PORTRAIT_CLEANUP_OPTIN02 pending queueRootGone=" + (queueRoot == null)
                    + " studioRootGone=" + (studioRoot == null) + " studioGone=" + (portrait == null)
                    + " cameraGone=" + (camera == null) + " targetGone=" + (target == null)
                    + " bodyGone=" + (body == null) + " revisionGone=" + (revision == null));

                Assert.That(portrait == null && body == null && revision == null && target == null, Is.True,
                    "Queue teardown must run the actual studio's pending-revision and render-target cleanup, not only discard its queue field.");
                Assert.That(appearance.ContentKey(), Is.EqualTo(key));
                Assert.That(preview != null && ownedTexture != null && ownedTexture.IsCreated(), Is.True);
                AssertUnrelatedResourcesSurvive();
                InvokeQueueShutdown(queue);
                AssertUnrelatedResourcesSurvive();
                Object.DestroyImmediate(queueRoot);
                LogAssert.NoUnexpectedReceived();
            }
            finally { DestroyPortraitFixture(queueRoot, portrait, portraitTarget); }
        }

        private static void StartQueuedPortrait(CharacterPortraitQueue queue, string key, CharacterAppearance appearance, out CharacterStudioPreview actual)
        {
            actual = null;
            queue.Enqueue(key, appearance);
            var update = typeof(CharacterPortraitQueue).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
            Assert.That(update, Is.Not.Null, "EditMode has no ordinary MonoBehaviour Update loop; invoke the existing queue producer once.");
            var field = typeof(CharacterPortraitQueue).GetField("preview", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            try { update.Invoke(queue, null); }
            finally { actual = field.GetValue(queue) as CharacterStudioPreview; }
            Assert.That(actual != null && actual.gameObject != queue.gameObject
                && (actual.gameObject.hideFlags & HideFlags.DontSave) == HideFlags.DontSave, Is.True,
                "Observe the exact real transient studio created by this queue; never inject an owner or search foreign objects.");
        }

        private static void InvokeQueueShutdown(CharacterPortraitQueue queue)
        {
            var shutdown = typeof(CharacterPortraitQueue).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
            Assert.That(shutdown, Is.Not.Null, "Invoke the existing real queue cleanup method; do not assume EditMode lifecycle dispatch.");
            shutdown.Invoke(queue, null);
        }

        private static void DestroyPortraitFixture(GameObject queueRoot, CharacterStudioPreview portrait, RenderTexture portraitTarget)
        {
            try { if (queueRoot != null) Object.DestroyImmediate(queueRoot); }
            finally
            {
                // Also release the held studio on the original red baseline where Queue.OnDestroy rejected Destroy.
                try { if (portrait != null) Object.DestroyImmediate(portrait.gameObject); }
                finally
                {
                    // A failed nested cleanup assertion must not leak this exact test-owned native target.
                    if (portraitTarget != null) { portraitTarget.Release(); Object.DestroyImmediate(portraitTarget); }
                }
            }
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
