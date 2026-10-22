using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>Real scene unloads retire transient portrait workers, not cached pixels or providers.</summary>
    public sealed class CharacterPortraitSceneLifetimePlayModeTests
    {
        private Scene original, owner, unrelated;
        private ICharacterBodyProvider previousProvider;
        private RecordingProvider provider;
        private CharacterStudioPreview foreignStudio;
        private RenderTexture foreignTexture;
        private AnimatorOverrideController borrowedController;
        private UnityAction<Scene> unloadedObserver;
        private bool ownsScope;
        private readonly List<CharacterPortraitQueue> queues = new List<CharacterPortraitQueue>();
        private readonly List<CharacterStudioPreview> previews = new List<CharacterStudioPreview>();
        private readonly List<RenderTexture> targets = new List<RenderTexture>();

        [SetUp]
        public void SetUp()
        {
            ownsScope = false;
            original = SceneManager.GetActiveScene();
            previousProvider = CharacterBodySource.Provider;
            Assert.That(object.ReferenceEquals(CurrentQueue(), null), Is.True,
                "Run these fixtures in their own fresh process; do not adopt or destroy a prior portrait worker.");
            foreach (string field in new[] { "Cache", "CacheOrder", "OrderNodes", "Temporary" })
                Assert.That(((ICollection)Field(typeof(CharacterPortraits), field).GetValue(null)).Count, Is.Zero,
                    "Refuse foreign portrait state before acquiring fixture ownership: " + field);
            Assert.That(previousProvider, Is.Null, "Do not replace or adopt a pre-existing provider.");
            ownsScope = true;
            queues.Clear(); previews.Clear(); targets.Clear();
            provider = new RecordingProvider();
            CharacterBodySource.Register(provider);
            borrowedController = new AnimatorOverrideController { name = "Borrowed portrait fixture controller" };
            provider.BorrowedController = borrowedController;
            foreignStudio = CharacterStudioPreview.Create("Unrelated portrait lifetime studio");
            foreignTexture = (RenderTexture)foreignStudio.Texture;
            owner = SceneManager.CreateScene("Portrait owner " + Guid.NewGuid().ToString("N"));
            unrelated = SceneManager.CreateScene("Portrait unrelated " + Guid.NewGuid().ToString("N"));
            Assert.That(SceneManager.SetActiveScene(owner), Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!ownsScope) yield break; // A failed precondition grants no authority over process-wide resources.
            ownsScope = false;
            if (unloadedObserver != null) SceneManager.sceneUnloaded -= unloadedObserver;
            unloadedObserver = null;
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            CharacterPortraits.Release(); // Exclusive empty-baseline fixture cleanup, never the unload action under test.
            CharacterBodySource.Unregister(provider);
            foreach (var queue in queues) if (queue != null) Object.Destroy(queue.gameObject);
            foreach (var preview in previews) if (preview != null) Object.Destroy(preview.gameObject);
            if (foreignStudio != null) Object.Destroy(foreignStudio.gameObject);
            if (borrowedController != null) Object.Destroy(borrowedController);
            if (owner.IsValid() && owner.isLoaded) yield return Unload(owner);
            if (unrelated.IsValid() && unrelated.isLoaded) yield return Unload(unrelated);
            yield return null;
            // Exact held targets also clean a failing baseline; never scan or destroy foreign objects.
            foreach (var target in targets)
                if (target != null) { target.Release(); Object.Destroy(target); }
            if (foreignTexture != null) { foreignTexture.Release(); Object.Destroy(foreignTexture); }
            yield return null;
            Assert.That(CharacterBodySource.Provider, Is.SameAs(previousProvider));
        }

        [UnityTest]
        public IEnumerator PendingPortraitRetiresOnlyWhenItsCreationSceneUnloadsAndCanBeRequestedAgain()
        {
            var request = Request("player");
            Assert.That(CharacterPortraits.Get(request), Is.Null);
            var queue = TrackQueue();
            CharacterPortraits.Get(Request("emma-brown"));
            yield return Until(() => provider.Roots.Count == 1);
            var preview = TrackPreview(queue);
            var body = provider.Roots[0];
            var revision = body.transform.parent.gameObject;
            var target = (RenderTexture)preview.Texture;
            Assert.That(preview.IsBuilding, Is.True);
            Assert.That(body.GetComponent<CharacterBodyBuildState>().Ready, Is.False);
            Assert.That(provider.Last.Purpose, Is.EqualTo(CharacterBuildPurpose.Studio));

            Assert.That(SceneManager.SetActiveScene(original), Is.True);
            yield return Unload(unrelated);
            Assert.That(CurrentQueue(), Is.SameAs(queue), "An unrelated unload is not this worker's lifetime boundary.");
            Assert.That(preview != null && body != null && revision != null && target.IsCreated(), Is.True);
            Assert.That(provider.Roots.Count, Is.EqualTo(1), "A pending build still serializes later requests.");

            yield return Unload(owner);
            Assert.That(queue == null && preview == null && body == null && revision == null && target == null, Is.True,
                "Awaited owning-scene unload plus one cleanup frame retires the exact pending hierarchy and target.");
            Assert.That(object.ReferenceEquals(CurrentQueue(), null), Is.True, "Scene retirement detaches its own static worker reference, not only Unity's fake null.");
            Assert.That(provider.Roots.Count, Is.EqualTo(1), "Queued work cannot start after retirement.");
            AssertBorrowedResourcesSurvive();

            CharacterPortraits.Get(request);
            var replacement = TrackQueue();
            Assert.That(object.ReferenceEquals(queue, replacement), Is.False);
            yield return Until(() => provider.Roots.Count == 2);
            TrackPreview(replacement);
            Assert.That(provider.Last.Appearance.ContentKey(), Is.EqualTo(request.Key.Substring("appearance:".Length)),
                "Cancelling a scene is not a failed-look back-off: the same appearance can be requested normally.");
            AssertBorrowedResourcesSurvive();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CompletedPixelsSurviveUnloadAndOldDestructionCannotDetachTheNextQueue()
        {
            provider.CompleteImmediately = true;
            var completedRequest = Request("player");
            CharacterPortraits.Get(completedRequest);
            var oldQueue = TrackQueue();
            Texture pixels = null;
            yield return Until(() => (pixels = CharacterPortraits.Get(completedRequest)) != null);
            var oldPreview = TrackPreview(oldQueue);
            var oldBody = provider.Roots[0];
            var oldTarget = (RenderTexture)oldPreview.Texture;
            Assert.That(oldPreview.IsBuilding, Is.False);
            Assert.That(pixels is RenderTexture && ((RenderTexture)pixels).IsCreated(), Is.True);

            var nextRequest = Request("emma-brown");
            CharacterPortraitQueue replacement = null;
            Exception callbackFailure = null;
            bool observed = false;
            int retiredPendingCount = -1;
            // Registered after the queue's owner handler. Demand in this callback happens before
            // the retired root's deferred OnDestroy, which must not detach the replacement.
            unloadedObserver = scene =>
            {
                if (scene != owner) return;
                observed = true;
                try
                {
                    oldQueue.Enqueue("late-retired-request", CharacterAppearance.Preset("player"));
                    retiredPendingCount = ((ICollection)Field(typeof(CharacterPortraitQueue), "pending").GetValue(oldQueue)).Count;
                    CharacterPortraits.Get(nextRequest);
                    replacement = TrackQueue();
                }
                catch (Exception exception) { callbackFailure = exception; }
            };
            SceneManager.sceneUnloaded += unloadedObserver;
            Assert.That(SceneManager.SetActiveScene(original), Is.True);
            yield return Unload(owner);
            Assert.That(observed, Is.True, "The test must observe the actual owner scene's unload event.");
            Assert.That(callbackFailure, Is.Null);
            Assert.That(retiredPendingCount, Is.Zero, "A retired worker rejects late demand before its deferred destruction.");
            Assert.That(oldQueue == null && oldPreview == null && oldBody == null && oldTarget == null, Is.True);
            Assert.That(replacement != null && !object.ReferenceEquals(replacement, oldQueue), Is.True);
            Assert.That(CurrentQueue(), Is.SameAs(replacement), "The previous worker's OnDestroy cannot clear its successor.");
            Assert.That(CharacterPortraits.Get(completedRequest), Is.SameAs(pixels), "Scene retirement preserves completed portrait pixels.");
            Assert.That(((RenderTexture)pixels).IsCreated(), Is.True);
            AssertBorrowedResourcesSurvive();

            yield return Until(() => CharacterPortraits.Get(nextRequest) != null);
            TrackPreview(replacement);
            Assert.That(provider.Roots.Count, Is.EqualTo(2), "The next scene builds only its new look, not the retained portrait.");
            Assert.That(CurrentQueue(), Is.SameAs(replacement));
            Assert.That(CharacterPortraits.Get(completedRequest), Is.SameAs(pixels));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator QueueOwnerBindingRejectsInvalidAndDifferentScenesWithoutChangingItsFirstOwner()
        {
            var root = new GameObject("Explicit owner binding fixture");
            var actualCreationScene = root.scene;
            root.hideFlags = HideFlags.DontSave;
            var queue = root.AddComponent<CharacterPortraitQueue>();
            queues.Add(queue);
            Assert.That(SceneManager.SetActiveScene(unrelated), Is.True);
            var bind = typeof(CharacterPortraitQueue).GetMethod("BindOwnerScene", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(bind, Is.Not.Null);
            var handler = typeof(CharacterPortraitQueue).GetMethod("OnOwnerSceneUnloaded", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(handler, Is.Not.Null);
            var retainedHandler = (UnityAction<Scene>)Delegate.CreateDelegate(typeof(UnityAction<Scene>), queue, handler);
            Assert.That((bool)bind.Invoke(queue, new object[] { default(Scene) }), Is.False);
            Assert.That((bool)bind.Invoke(queue, new object[] { actualCreationScene }), Is.True,
                "Bind the captured creation scene, not whichever scene is active later.");
            Assert.That((bool)bind.Invoke(queue, new object[] { actualCreationScene }), Is.True, "Repeated identical binding is idempotent.");
            Assert.That((bool)bind.Invoke(queue, new object[] { unrelated }), Is.False, "A later caller cannot take over its owner.");
            Assert.That(SceneManager.SetActiveScene(original), Is.True);
            yield return Unload(unrelated);
            Assert.That(queue != null, Is.True, "Rejecting a rebind must keep the original lifetime contract.");
            yield return Unload(owner);
            Assert.That(queue == null, Is.True);
            CharacterPortraits.Get(Request("player"));
            var replacement = TrackQueue();
            // A multicast invocation may already have captured this callback before retirement
            // unsubscribes it. Its managed target can therefore outlive the native component.
            Assert.DoesNotThrow(() => retainedHandler(actualCreationScene));
            Assert.DoesNotThrow(() => retainedHandler(actualCreationScene), "Late delivery is idempotent too.");
            Assert.That(CurrentQueue(), Is.SameAs(replacement), "A retired delegate cannot detach or destroy a later worker.");
            Assert.That(replacement != null, Is.True);
            AssertBorrowedResourcesSurvive();
            LogAssert.NoUnexpectedReceived();
        }

        private static CharacterPortraits.PortraitRequest Request(string preset) =>
            CharacterPortraits.PrepareAppearance(CharacterAppearance.Preset(preset));

        private static FieldInfo Field(Type type, string name)
        {
            var field = type.GetField(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Observe an exact owned handle; never scan or inject a replacement.");
            return field;
        }

        private static CharacterPortraitQueue CurrentQueue() =>
            (CharacterPortraitQueue)Field(typeof(CharacterPortraits), "queue").GetValue(null);

        private CharacterPortraitQueue TrackQueue()
        {
            var queue = CurrentQueue();
            Assert.That(queue != null, Is.True);
            if (!queues.Contains(queue)) queues.Add(queue);
            return queue;
        }

        private CharacterStudioPreview TrackPreview(CharacterPortraitQueue queue)
        {
            var preview = (CharacterStudioPreview)Field(typeof(CharacterPortraitQueue), "preview").GetValue(queue);
            Assert.That(preview != null, Is.True);
            if (!previews.Contains(preview)) previews.Add(preview);
            var target = (RenderTexture)preview.Texture;
            if (!targets.Contains(target)) targets.Add(target);
            return preview;
        }

        private void AssertBorrowedResourcesSurvive()
        {
            Assert.That(CharacterBodySource.Provider, Is.SameAs(provider));
            Assert.That(borrowedController != null && provider.BorrowedController == borrowedController, Is.True);
            Assert.That(foreignStudio != null && foreignTexture != null && foreignTexture.IsCreated(), Is.True);
            Assert.That(foreignStudio.Texture, Is.SameAs(foreignTexture));
        }

        private static IEnumerator Until(Func<bool> condition)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 4;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(condition(), Is.True, "Bounded portrait fixture operation did not complete.");
        }

        private static IEnumerator Unload(Scene scene)
        {
            var operation = SceneManager.UnloadSceneAsync(scene);
            Assert.That(operation, Is.Not.Null);
            yield return Until(() => operation.isDone);
            yield return null; // Exactly one ordinary cleanup frame after the awaited unload.
        }

        private sealed class RecordingProvider : IModularCharacterBodyProvider
        {
            public bool CompleteImmediately;
            public RuntimeAnimatorController BorrowedController;
            public CharacterBodyRequest Last;
            public readonly List<GameObject> Roots = new List<GameObject>();
            public ICharacterAppearanceCatalog Catalog => null;

            public bool TryCreate(in CharacterBodyRequest request, Transform parent, Color color, out CharacterBody body)
            {
                Last = request;
                var root = new GameObject("Owned portrait lifecycle body");
                root.transform.SetParent(parent, false);
                // A renderer is enough for the real studio completion path. No primitive, mesh,
                // Animator or graph is needed to test scene ownership and retained pixels.
                root.AddComponent<MeshRenderer>();
                var state = root.AddComponent<CharacterBodyBuildState>();
                state.Revision = request.Revision; state.Ready = CompleteImmediately;
                Roots.Add(root);
                body = new CharacterBody(root, null, deferred: true);
                return true;
            }

            public bool TryCreate(string id, Transform parent, Color color, out CharacterBody body)
            { body = default; throw new InvalidOperationException("The studio must use an explicit appearance request."); }
            public void SetWardrobeColor(in CharacterBody body, Color color) { }
        }
    }
}
