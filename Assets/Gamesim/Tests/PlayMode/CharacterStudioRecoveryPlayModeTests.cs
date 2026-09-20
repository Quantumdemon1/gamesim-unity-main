using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    public sealed class CharacterStudioRecoveryPlayModeTests
    {
        private CharacterStudioPreview preview;
        private DeferredProvider provider;

        [SetUp]
        public void SetUp()
        {
            provider = new DeferredProvider();
            CharacterBodySource.Register(provider);
            preview = CharacterStudioPreview.Create("Recovery test studio");
            preview.BuildTimeoutSeconds = .1f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            CharacterBodySource.Unregister(provider);
            CharacterPortraits.Release();
            if (preview != null) Object.Destroy(preview.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TemporaryPortraitRecoversWithoutStarvingAnotherQueuedHouseguest()
        {
            CharacterPortraits.Release();
            provider.ThrowAfterAllocation = true;
            var broken = CharacterPortraits.PrepareAppearance(CharacterAppearance.Preset("player"));
            Texture temporary = null;
            yield return Until(() => (temporary = CharacterPortraits.Get(broken)) != null);
            provider.ThrowAfterAllocation = false;
            provider.CompleteImmediately = true;
            var another = CharacterPortraits.PrepareAppearance(CharacterAppearance.Preset("emma-brown"));
            Texture healthy = null;
            yield return Until(() => (healthy = CharacterPortraits.Get(another)) != null);
            Assert.That(healthy, Is.Not.Null, "An earlier failed portrait cannot block the remaining cast.");
            Texture recovered = null;
            yield return Until(() => (recovered = CharacterPortraits.Get(broken)) != null && recovered != temporary);
            Assert.That(temporary == null || !(temporary as RenderTexture).IsCreated(), Is.True, "Replacing a placeholder releases its texture.");
            Assert.That(recovered, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator PreparedPortraitsOwnTheirSnapshotAndSurviveCacheInvalidation()
        {
            CharacterPortraits.Release();
            provider.CompleteImmediately = true;
            var contestant = ContentCatalog.Create(17).Find(ContentCatalog.PlayerId);
            contestant.appearance = CharacterAppearance.Preset("player");
            contestant.appearance.outfits.Add(new CharacterOutfit { id = "Everyday" });
            contestant.appearance.outfits.Add(new CharacterOutfit { id = "Formal" });
            contestant.appearance.activeOutfit = "Formal";
            AppearanceEditing.SetColor(contestant.appearance, "Hair", new Color(.2f, .3f, .4f));
            var prepared = CharacterPortraits.Prepare(contestant);
            var explicitOutfit = CharacterPortraits.PrepareAppearance(contestant.appearance);
            string key = prepared.Key;
            Assert.That(explicitOutfit.Key, Is.Not.EqualTo(prepared.Key), "Gameplay headshots use Everyday; explicit previews retain Formal.");
            contestant.appearance.colors[0].r = .9f;
            contestant.appearance.activeOutfit = "Everyday";
            Texture first = null;
            yield return Until(() => (first = CharacterPortraits.Get(prepared)) != null);
            Assert.That(provider.Last.Appearance.activeOutfit, Is.EqualTo("Everyday"));
            Assert.That(provider.Last.Appearance.colors.Single(value => value.id == "Hair").r, Is.EqualTo(.2f));
            Assert.That(prepared.Key, Is.SameAs(key), "Polling reuses the prepared key.");
            Assert.That(CharacterPortraits.Prepare(contestant).Key, Is.Not.EqualTo(key), "A new binding sees subsequent edits.");
            for (int i = 0; i < 100; i++) Assert.That(CharacterPortraits.Get(prepared), Is.SameAs(first));
            int builtBeforeRelease = provider.Roots.Count;
            CharacterPortraits.Release();
            yield return null;
            Texture rebuilt = null;
            yield return Until(() => (rebuilt = CharacterPortraits.Get(prepared)) != null);
            Assert.That(provider.Roots.Count, Is.GreaterThan(builtBeforeRelease));
            Assert.That(first == null, Is.True);
            Assert.That(rebuilt, Is.Not.Null);
            yield return Until(() => CharacterPortraits.Get(explicitOutfit) != null);
            Assert.That(provider.Last.Appearance.activeOutfit, Is.EqualTo("Formal"));
            Assert.That(provider.Last.Appearance.colors.Single(value => value.id == "Hair").r, Is.EqualTo(.2f));
        }

        [UnityTest]
        public IEnumerator VisibleBindingReacquiresItsPortraitAfterLruEvictionWithoutChangingTint()
        {
            CharacterPortraits.Release();
            provider.CompleteImmediately = true;
            var view = new GameObject("Evicted portrait binding", typeof(RectTransform), typeof(RawImage));
            try
            {
                var image = view.GetComponent<RawImage>();
                var tint = new Color(.3f, .3f, .3f, .8f);
                image.color = tint;
                CharacterPortraits.Bind(image, ContentCatalog.Create(3).Find(ContentCatalog.PlayerId));
                yield return Until(() => image.texture != null);
                var original = image.texture;
                // Seed completed captures through the cache's insertion path; building 97 unrelated avatars
                // would add delay without improving the eviction/binding behavior this test exercises.
                var store = typeof(CharacterPortraits).GetMethod("StoreAppearance", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(store, Is.Not.Null);
                for (int i = 0; i < 97; i++)
                    store.Invoke(null, new object[] { "eviction-fixture-" + i, Texture2D.whiteTexture, false });
                yield return null;
                Assert.That(original == null, Is.True, "The least recently used texture must be released.");
                yield return Until(() => image.texture != null && image.texture != original);
                Assert.That(image.color, Is.EqualTo(tint));
                Assert.That(image.enabled, Is.True);
            }
            finally { Object.Destroy(view); }
        }

        [UnityTest]
        public IEnumerator TimedOutBuildUsesFallbackAndRetryRecoversTheSameSavedAppearance()
        {
            var appearance = CharacterAppearance.Preset("emma-brown");
            string key = appearance.ContentKey();
            preview.Show(appearance);
            yield return Until(() => preview.CanRetry && !preview.IsBuilding);
            Assert.That(preview.Status, Does.Contain("too long"));
            Assert.That(preview.CompletedKey, Is.EqualTo(key));
            Assert.That(provider.Roots[0] == null, Is.True, "Timed-out revision must release its entire body.");
            provider.CompleteImmediately = true;
            preview.Retry();
            yield return Until(() => !preview.IsBuilding);
            Assert.That(provider.Roots.Count, Is.EqualTo(2));
            Assert.That(preview.CanRetry, Is.False);
            Assert.That(preview.Status, Is.EqualTo("Preview ready"));
            Assert.That(preview.CompletedKey, Is.EqualTo(key));
            Assert.That(appearance.ContentKey(), Is.EqualTo(key), "Recovery cannot overwrite saved customizations.");
        }

        [UnityTest]
        public IEnumerator ProviderExceptionReleasesPartiallyAllocatedChildrenAndOffersRetry()
        {
            provider.ThrowAfterAllocation = true;
            preview.Show(CharacterAppearance.Preset("player"));
            yield return Until(() => preview.CanRetry && !preview.IsBuilding);
            Assert.That(preview.Status, Does.Contain("could not be built"));
            Assert.That(provider.Roots[0] == null, Is.True);
            Assert.That(preview.GetComponentsInChildren<Renderer>(), Is.Not.Empty, "Recovery shows a usable authored fallback.");
        }

        [UnityTest]
        public IEnumerator AReadyFlagWithoutRenderersAlsoTimesOut()
        {
            provider.NoRenderer = true;
            provider.CompleteImmediately = true;
            preview.Show(CharacterAppearance.Preset("player"));
            yield return Until(() => preview.CanRetry && !preview.IsBuilding);
            Assert.That(preview.Status, Does.Contain("too long"));
            Assert.That(provider.Roots[0] == null, Is.True);
        }

        [UnityTest]
        public IEnumerator NewestRevisionWinsAndDisablingCancelsPendingWork()
        {
            preview.BuildTimeoutSeconds = 3f;
            var oldAppearance = CharacterAppearance.Preset("player");
            preview.Show(oldAppearance);
            yield return Until(() => provider.Roots.Count == 1);
            var oldBody = provider.Roots[0];
            var changed = oldAppearance.Clone();
            AppearanceEditing.SetValue(changed, "height", .29f);
            provider.CompleteImmediately = true;
            preview.Show(changed);
            // A late completion from the previous request cannot publish its pixels as the new revision.
            oldBody.GetComponent<CharacterBodyBuildState>().Ready = true;
            yield return Until(() => !preview.IsBuilding);
            Assert.That(oldBody == null, Is.True);
            Assert.That(preview.CompletedKey, Is.EqualTo(changed.ContentKey()));
            provider.CompleteImmediately = false;
            AppearanceEditing.SetValue(changed, "height", .31f);
            preview.Show(changed);
            yield return Until(() => provider.Roots.Count == 3);
            var pendingBody = provider.Roots[2];
            preview.gameObject.SetActive(false);
            yield return null;
            Assert.That(preview.IsBuilding, Is.False);
            Assert.That(pendingBody == null, Is.True);
            provider.CompleteImmediately = true;
            preview.gameObject.SetActive(true);
            yield return Until(() => !preview.IsBuilding);
            Assert.That(preview.CompletedKey, Is.EqualTo(changed.ContentKey()));
            var texture = preview.Texture as RenderTexture;
            Object.Destroy(preview.gameObject);
            yield return null;
            Assert.That(texture == null || !texture.IsCreated(), Is.True, "Closing releases the render target.");
        }

        private static IEnumerator Until(Func<bool> condition)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 4;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(condition(), Is.True, "Bounded preview operation did not complete.");
            yield return null; // Deferred Destroy has now completed too.
        }

        private sealed class DeferredProvider : IModularCharacterBodyProvider
        {
            public bool CompleteImmediately, ThrowAfterAllocation, NoRenderer;
            public CharacterBodyRequest Last;
            public readonly List<GameObject> Roots = new List<GameObject>();
            public ICharacterAppearanceCatalog Catalog => null;
            public bool TryCreate(in CharacterBodyRequest request, Transform parent, Color color, out CharacterBody body)
            {
                Last = request;
                var root = NoRenderer ? new GameObject("Empty pending body") : GameObject.CreatePrimitive(PrimitiveType.Capsule);
                root.transform.SetParent(parent, false);
                Roots.Add(root);
                if (ThrowAfterAllocation) throw new InvalidOperationException("Test provider failed after allocation");
                var state = root.AddComponent<CharacterBodyBuildState>();
                state.Revision = request.Revision; state.Ready = CompleteImmediately;
                body = new CharacterBody(root, null, true);
                return true;
            }
            public bool TryCreate(string id, Transform parent, Color color, out CharacterBody body)
            { body = default; throw new InvalidOperationException("Explicit appearance request required."); }
            public void SetWardrobeColor(in CharacterBody body, Color color) { }
        }
    }
}
